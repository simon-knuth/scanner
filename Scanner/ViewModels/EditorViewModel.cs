using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Input;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;
using static Enums;
using static Utilities;

namespace Scanner.ViewModels
{
    public class EditorViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetRequiredService<IAppCenterService>();
        private readonly ILogService LogService = Ioc.Default.GetRequiredService<ILogService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        public readonly IScanService ScanService = Ioc.Default.GetRequiredService<IScanService>();
        public readonly IScanResultService ScanResultService = Ioc.Default.GetRequiredService<IScanResultService>();

        public AsyncRelayCommand<ImageCropper> CropPageCommand;
        public AsyncRelayCommand<ImageCropper> CropPagesCommand;
        public AsyncRelayCommand<ImageCropper> CropPageAsCopyCommand;
        public AsyncRelayCommand<InkCanvas> DrawOnPageCommand;
        public AsyncRelayCommand<InkCanvas> DrawOnPageAsCopyCommand;
        public AsyncRelayCommand<string> RotatePageCommand;
        public AsyncRelayCommand<string> RenameCommand;
        public AsyncRelayCommand DeleteCommand;
        public AsyncRelayCommand CopyCommand;
        public AsyncRelayCommand<string> OpenWithCommand;
        public RelayCommand ShareCommand;
        public RelayCommand EnterCropModeCommand;
        public RelayCommand LeaveCropModeCommand;
        public RelayCommand EnterDrawModeCommand;
        public RelayCommand LeaveDrawModeCommand;
        public RelayCommand<string> AspectRatioCommand;
        public RelayCommand<Rect> AspectRatioFlipCommand;

        public event EventHandler CropSuccessful;
        public event EventHandler CropAsCopySuccessful;
        public event EventHandler RotateSuccessful;
        public event EventHandler DrawSuccessful;
        public event EventHandler DrawAsCopySuccessful;
        public event EventHandler RenameSuccessful;
        public event EventHandler DeleteSuccessful;
        public event EventHandler CopySuccessful;

        public event EventHandler TargetedShareUiRequested;

        private Orientation _Orientation;
        public Orientation Orientation
        {
            get => _Orientation;
            set => SetProperty(ref _Orientation, value);
        }

        private bool _ShowAnimations;
        public bool ShowAnimations
        {
            get => _ShowAnimations;
            set => SetProperty(ref _ShowAnimations, value);
        }

        private ScanResult _ScanResult;
        public ScanResult ScanResult
        {
            get => _ScanResult;
            set => SetProperty(ref _ScanResult, value);
        }

        private ScanResultElement _SelectedPage;
        public ScanResultElement SelectedPage
        {
            get => _SelectedPage;
            set
            {
                SetProperty(ref _SelectedPage, value);
                BroadcastSelectedPageTitle();
            }
        }

        private int _SelectedPageIndex;
        public int SelectedPageIndex
        {
            get => _SelectedPageIndex;
            set
            {
                int old = _SelectedPageIndex;

                if (old != value)
                {
                    SetProperty(ref _SelectedPageIndex, value);
                    if (value != -1) Messenger.Send(new EditorCurrentIndexChangedMessage(value));
                    RefreshSelectedPageText();
                }
            }
        }

        private string _SelectedPageText;
        public string SelectedPageText
        {
            get => _SelectedPageText;
            set => SetProperty(ref _SelectedPageText, value);
        }

        private EditorMode _EditorMode = EditorMode.Initial;
        public EditorMode EditorMode
        {
            get => _EditorMode;
            set
            {
                SetProperty(ref _EditorMode, value);
                if (value == EditorMode.Initial)
                {
                    IsEditing = false;
                }
                else
                {
                    IsEditing = true;

                    if (value == EditorMode.Crop)
                    {
                        RefreshSimilarPagesForCrop();
                    }
                }
            }
        }

        private bool _IsScanResultChanging;
        public bool IsScanResultChanging
        {
            get => _IsScanResultChanging;
            set
            {
                SetProperty(ref _IsScanResultChanging, value);
                if (value == false)
                {
                    RefreshSelectedPageText();
                    if (EditorMode == EditorMode.Crop) RefreshSimilarPagesForCrop();
                }
            }
        }

        private bool _IsScanning;
        public bool IsScanning
        {
            get => _IsScanning;
            set => SetProperty(ref _IsScanning, value);
        }

        private bool _IsEditing;
        public bool IsEditing
        {
            get => _IsEditing;
            set
            {
                bool old = _IsEditing;

                if (old != value)
                {
                    SetProperty(ref _IsEditing, value);
                    Messenger.Send(new EditorIsEditingChangedMessage(value));
                }
            }
        }

        private List<OpenWithApp> _OpenWithApps;
        public List<OpenWithApp> OpenWithApps
        {
            get => _OpenWithApps;
            set => SetProperty(ref _OpenWithApps, value);
        }

        private AspectRatioOption _SelectedAspectRatio;
        public AspectRatioOption SelectedAspectRatio
        {
            get => _SelectedAspectRatio;
            set
            {
                SetProperty(ref _SelectedAspectRatio, value);
                SelectedAspectRatioValue = ConvertAspectRatioOptionToValue(value);

                if (value == AspectRatioOption.Custom)
                {
                    IsFixedAspectRatioSelected = false;
                }
                else
                {
                    IsFixedAspectRatioSelected = true;
                }

                SettingsService.SetSetting(AppSetting.LastUsedCropAspectRatio, value);
            }
        }

        private double? _SelectedAspectRatioValue;
        public double? SelectedAspectRatioValue
        {
            get => _SelectedAspectRatioValue;
            set => SetProperty(ref _SelectedAspectRatioValue, value);
        }

        private bool _IsFixedAspectRatioSelected;
        public bool IsFixedAspectRatioSelected
        {
            get => _IsFixedAspectRatioSelected;
            set => SetProperty(ref _IsFixedAspectRatioSelected, value);
        }

        private bool _IsDeviceTouchEnabled;
        public bool IsDeviceTouchEnabled
        {
            get => _IsDeviceTouchEnabled;
            set => SetProperty(ref _IsDeviceTouchEnabled, value);
        }

        private bool _IsTouchDrawingEnabled;
        public bool IsTouchDrawingEnabled
        {
            get => _IsTouchDrawingEnabled;
            set
            {
                SetProperty(ref _IsTouchDrawingEnabled, value);
                SettingsService.SetSetting(AppSetting.LastTouchDrawState, value);
            }
        }

        private List<int> _SimilarPageIndicesForCrop;
        public List<int> SimilarPageIndicesForCrop
        {
            get => _SimilarPageIndicesForCrop;
            set => SetProperty(ref _SimilarPageIndicesForCrop, value);
        }

        private List<ScanResultElement> _SimilarPagesForCrop;
        public List<ScanResultElement> SimilarPagesForCrop
        {
            get => _SimilarPagesForCrop;
            set => SetProperty(ref _SimilarPagesForCrop, value);
        }

        private bool _ShowOpenWithWarning;
        public bool ShowOpenWithWarning
        {
            get => _ShowOpenWithWarning;
            set
            {
                SetProperty(ref _ShowOpenWithWarning, value);
                SettingsService.SetSetting(AppSetting.ShowOpenWithWarning, value);
            }
        }

        private IReadOnlyList<ItemIndexRange> _SelectedRangesCropSimilarPages;
        public IReadOnlyList<ItemIndexRange> SelectedRangesCropSimilarPages
        {
            get => _SelectedRangesCropSimilarPages;
            set => SetProperty(ref _SelectedRangesCropSimilarPages, value);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public EditorViewModel()
        {
            RefreshOrientationSetting();
            RefreshAnimationsSetting();
            SettingsService.SettingChanged += SettingsService_SettingChanged;
            ShowOpenWithWarning = (bool)SettingsService.GetSetting(AppSetting.ShowOpenWithWarning);

            ScanResultService.ScanResultCreated += ScanResultService_ScanResultCreated;
            ScanResultService.ScanResultDismissed += ScanResultService_ScanResultDismissed;
            IsScanResultChanging = ScanResultService.IsScanResultChanging;
            ScanResultService.ScanResultChanging += (x, y) => IsScanResultChanging = true;
            ScanResultService.ScanResultChanged += (x, y) => IsScanResultChanging = false;

            IsScanning = ScanService.IsScanInProgress;
            ScanService.ScanStarted += (x, y) => IsScanning = true;
            ScanService.ScanEnded += ScanService_ScanEnded;

            Messenger.Register<EditorCurrentIndexRequestMessage>(this, (r, m) => m.Reply(SelectedPageIndex));
            Messenger.Register<PageListCurrentIndexChangedMessage>(this, (r, m) => SelectedPageIndex = m.Value);
            Messenger.Register<EditorIsEditingRequestMessage>(this, (r, m) => m.Reply(IsEditing));

            CropPageCommand = new AsyncRelayCommand<ImageCropper>((x) => CropAsync(x));
            CropPagesCommand = new AsyncRelayCommand<ImageCropper>((x) => CropSimilarPagesAsync(x));
            CropPageAsCopyCommand = new AsyncRelayCommand<ImageCropper>((x) => CropAsCopyAsync(x));
            DrawOnPageCommand = new AsyncRelayCommand<InkCanvas>((x) => DrawAsync(x));
            DrawOnPageAsCopyCommand = new AsyncRelayCommand<InkCanvas>((x) => DrawAsCopyAsync(x));
            RotatePageCommand = new AsyncRelayCommand<string>((x) => RotatePageAsync((BitmapRotation)int.Parse(x)));
            RenameCommand = new AsyncRelayCommand<string>((x) => RenameAsync(x));
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            CopyCommand = new AsyncRelayCommand(CopyAsync);
            OpenWithCommand = new AsyncRelayCommand<string>((x) => OpenWithAsync(x));
            ShareCommand = new RelayCommand(Share);
            EnterCropModeCommand = new RelayCommand(EnterCropMode);
            LeaveCropModeCommand = new RelayCommand(LeaveCropMode);
            EnterDrawModeCommand = new RelayCommand(EnterDrawMode);
            LeaveDrawModeCommand = new RelayCommand(LeaveDrawMode);
            AspectRatioCommand = new RelayCommand<string>((x) => SelectedAspectRatio = (AspectRatioOption)int.Parse(x));
            AspectRatioFlipCommand = new RelayCommand<Rect>((x) => FlipSelectedAspectRatio(x));

            IReadOnlyList<PointerDevice> pointerDevices = PointerDevice.GetPointerDevices();
            PointerDevice device = pointerDevices.FirstOrDefault((x) => x.PointerDeviceType == PointerDeviceType.Touch);
            IsDeviceTouchEnabled = device != null;

            SelectedAspectRatio = (AspectRatioOption)SettingsService.GetSetting(AppSetting.LastUsedCropAspectRatio);
            IsTouchDrawingEnabled = (bool)SettingsService.GetSetting(AppSetting.LastTouchDrawState);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void RefreshOrientationSetting()
        {
            int orientationValue = (int)SettingsService.GetSetting(AppSetting.SettingEditorOrientation);

            switch (orientationValue)
            {
                case 0:
                    Orientation = Orientation.Horizontal;
                    break;
                case 1:
                    Orientation = Orientation.Vertical;
                    break;
                default:
                    break;
            }
        }

        private void RefreshAnimationsSetting()
        {
            ShowAnimations = (bool)SettingsService.GetSetting(AppSetting.SettingAnimations);
        }

        private void SettingsService_SettingChanged(object sender, AppSetting e)
        {
            if (e == AppSetting.SettingEditorOrientation)
            {
                RefreshOrientationSetting();
            }
            else if (e == AppSetting.SettingAnimations)
            {
                RefreshAnimationsSetting();
            }
        }

        private void ScanResultService_ScanResultDismissed(object sender, EventArgs e)
        {
            ScanResult = null;
            SelectedPageIndex = -1;
        }

        private async void ScanResultService_ScanResultCreated(object sender, ScanResult scanResult)
        {
            ScanResult = scanResult;
            OpenWithApps = await GetAppsToOpenWith();
        }

        private void RefreshSelectedPageText()
        {
            if (SelectedPageIndex >= 0 && ScanResult != null)
            {
                SelectedPageText = String.Format(LocalizedString("TextPageIndicator"),
                    SelectedPageIndex + 1, ScanResult.NumberOfPages);
            }
            else
            {
                SelectedPageText = "";
            }
        }

        private void BroadcastSelectedPageTitle()
        {
            if (SelectedPage == null || SelectedPage.ScanFile == null) return;

            if (ScanResult.IsImage)
            {
                Messenger.Send(new EditorSelectionTitleChangedMessage
                {
                    Title = SelectedPage?.ScanFile?.Name
                });
            }
            else
            {
                Messenger.Send(new EditorSelectionTitleChangedMessage
                {
                    Title = ScanResult?.Pdf?.Name
                });
            }
        }

        private async Task RotatePageAsync(BitmapRotation rotation)
        {
            List<Tuple<int, BitmapRotation>> instructions = new List<Tuple<int, BitmapRotation>>
            {
                new Tuple<int, BitmapRotation>(SelectedPageIndex, rotation)
            };

            bool success = await ScanResultService.RotatePagesAsync(instructions);

            if (!success) return;

            RotateSuccessful?.Invoke(this, EventArgs.Empty);
            Messenger.Send(new NarratorAnnouncementMessage
            {
                AnnouncementText = LocalizedString("TextSavedChangesAccessibility")
            });
        }

        private async Task RenameAsync(string newName)
        {
            bool success;

            if (ScanResultService.Result.IsImage)
            {
                // rename single image file
                success = await ScanResultService.RenameAsync(SelectedPageIndex, newName);
            }
            else
            {
                // rename PDF document
                success = await ScanResultService.RenameAsync(newName);
            }

            if (!success) return;

            // broadcast new name
            BroadcastSelectedPageTitle();

            RenameSuccessful?.Invoke(this, EventArgs.Empty);
            Messenger.Send(new NarratorAnnouncementMessage
            {
                AnnouncementText = LocalizedString("TextSavedChangesAccessibility")
            });
        }

        private void EnterCropMode()
        {
            LogService?.Log.Information("EnterCropMode");
            EditorMode = EditorMode.Crop;
        }

        private void LeaveCropMode()
        {
            LogService?.Log.Information("LeaveCropMode");
            EditorMode = EditorMode.Initial;
        }

        private void EnterDrawMode()
        {
            LogService?.Log.Information("EnterDrawMode");
            EditorMode = EditorMode.Draw;
        }

        private void LeaveDrawMode()
        {
            LogService?.Log.Information("LeaveDrawMode");
            EditorMode = EditorMode.Initial;
        }

        private async Task CopyAsync()
        {
            bool success;

            if (ScanResultService.Result.IsImage)
            {
                // copy single image file
                success = await ScanResultService.CopyImageAsync(SelectedPageIndex);
            }
            else
            {
                // rename PDF document
                success = await ScanResultService.CopyAsync();
            }

            if (!success) return;

            CopySuccessful?.Invoke(this, EventArgs.Empty);
        }

        private void Share()
        {
            // collect files
            List<StorageFile> list = new List<StorageFile>();
            if (ScanResult.IsImage)
            {
                // share single image file
                list.Add(ScanResult.GetImageFile(SelectedPageIndex));
            }
            else
            {
                // share PDF document
                list.Add(ScanResult.Pdf);
            }

            // request share UI
            Messenger.Send(new SetShareFilesMessage { Files = list });
            TargetedShareUiRequested?.Invoke(this, EventArgs.Empty);

            AppCenterService?.TrackEvent(AppCenterEvent.Share);
        }

        private async Task OpenWithAsync(string appIndex)
        {
            int index = int.Parse(appIndex);

            if (index == -2)
            {
                // open the Microsoft Store to search for apps for this file extension
                ImageScannerFormat? format = ScanResult.GetFileFormat();
                if (format == null) return;

                string formatString = ConvertImageScannerFormatToString((ImageScannerFormat)format);
                await Launcher.LaunchUriAsync(new Uri($"ms-windows-store://assoc/?FileExt={formatString.Substring(1)}"));
            }

            bool successfullyOpened;
            if (ScanResultService.Result.IsImage)
            {
                // open single image file
                if (index == -1) successfullyOpened = await ScanResultService.OpenImageWithAsync(SelectedPageIndex);
                else successfullyOpened = await ScanResultService.OpenImageWithAsync(SelectedPageIndex, OpenWithApps[index].AppInfo);
            }
            else
            {
                // open PDF document
                if (index == -1) successfullyOpened = await ScanResultService.OpenWithAsync();
                else successfullyOpened = await ScanResultService.OpenWithAsync(OpenWithApps[index].AppInfo);
            }

            if (successfullyOpened)
            {
                ScanResultService.DismissScanResult();
                LogService?.Log.Information("OpenWithAsync: Success");
            }
            else
            {
                LogService?.Log.Information("OpenWithAsync: Failed to open in another app");
            }
        }

        private async Task DeleteAsync()
        {
            bool success;

            success = await ScanResultService.DeleteScanAsync(SelectedPageIndex);

            if (!success) return;

            DeleteSuccessful?.Invoke(this, EventArgs.Empty);
            Messenger.Send(new NarratorAnnouncementMessage
            {
                AnnouncementText = LocalizedString("TextSavedChangesAccessibility")
            });
        }

        private async Task<List<OpenWithApp>> GetAppsToOpenWith()
        {
            LogService?.Log.Information("GetAppsToOpenWith");
            List<OpenWithApp> result = new List<OpenWithApp>();

            // get format of current result
            ImageScannerFormat? format = ScanResult.GetFileFormat();
            if (format == null) return result;
            string formatString = ConvertImageScannerFormatToString((ImageScannerFormat)format);

            // find installed apps for this file extension
            IReadOnlyList<AppInfo> readOnlyList = await Launcher.FindFileHandlersAsync(formatString);
            foreach (AppInfo appInfo in readOnlyList)
            {
                LogService?.Log.Information($"GetAppsToOpenWith: Adding {appInfo.DisplayInfo.DisplayName}");

                try
                {
                    RandomAccessStreamReference stream = appInfo.DisplayInfo.GetLogo(new Size(64, 64));
                    IRandomAccessStreamWithContentType content = await stream.OpenReadAsync();

                    BitmapImage bmp = new BitmapImage();
                    await bmp.SetSourceAsync(content);

                    result.Add(new OpenWithApp
                    {
                        AppInfo = appInfo,
                        Logo = bmp
                    });
                }
                catch (Exception)
                {
                    // add without logo
                    result.Add(new OpenWithApp
                    {
                        AppInfo = appInfo
                    });
                }
                
                if (result.Count >= 4) break;   // 4 apps max
            }

            return result;
        }

        private void ScanService_ScanEnded(object sender, EventArgs e)
        {
            IsScanning = false;

            if (ScanResult != null)
            {
                ScanResultService.ScanResultChanged += ScanResultChanged_ScrollToNewPage;
            }
        }

        private void ScanResultChanged_ScrollToNewPage(object sender, EventArgs e)
        {
            ScanResultService.ScanResultChanged -= ScanResultChanged_ScrollToNewPage;
            if (ScanResult != null) SelectedPageIndex = ScanResult.NumberOfPages - 1;
        }

        private void FlipSelectedAspectRatio(Rect currentRect)
        {
            SelectedAspectRatioValue = currentRect.Height / currentRect.Width;
        }

        private async Task CropAsync(ImageCropper imageCropper)
        {
            bool success = await ScanResultService.CropScanAsync(SelectedPageIndex, imageCropper);

            if (!success) return;

            CropSuccessful?.Invoke(this, EventArgs.Empty);
            Messenger.Send(new NarratorAnnouncementMessage
            {
                AnnouncementText = LocalizedString("TextSavedChangesAccessibility")
            });
            LeaveCropMode();
        }

        private async Task CropSimilarPagesAsync(ImageCropper imageCropper)
        {
            List<int> indices = GetSelectedIndicesCropSimilarPages();
            indices.Add(SelectedPageIndex);

            bool success = await ScanResultService.CropScansAsync(indices, imageCropper.CroppedRegion);

            Messenger.Send(new NarratorAnnouncementMessage
            {
                AnnouncementText = LocalizedString("TextSavedChangesAccessibility")
            });
            LeaveCropMode();
        }

        private async Task CropAsCopyAsync(ImageCropper imageCropper)
        {
            bool success = await ScanResultService.CropScanAsCopyAsync(SelectedPageIndex, imageCropper);

            if (!success) return;

            CropAsCopySuccessful?.Invoke(this, EventArgs.Empty);
            Messenger.Send(new NarratorAnnouncementMessage
            {
                AnnouncementText = LocalizedString("TextSavedChangesCopyAccessibility")
            });
        }

        private async Task DrawAsync(InkCanvas inkCanvas)
        {
            bool success = await ScanResultService.DrawOnScanAsync(SelectedPageIndex, inkCanvas);

            if (!success) return;

            DrawSuccessful?.Invoke(this, EventArgs.Empty);
            Messenger.Send(new NarratorAnnouncementMessage
            {
                AnnouncementText = LocalizedString("TextSavedChangesAccessibility")
            });
            LeaveDrawMode();
        }

        private async Task DrawAsCopyAsync(InkCanvas inkCanvas)
        {
            bool success = await ScanResultService.DrawOnScanAsCopyAsync(SelectedPageIndex, inkCanvas);

            if (!success) return;

            DrawAsCopySuccessful?.Invoke(this, EventArgs.Empty);
            Messenger.Send(new NarratorAnnouncementMessage
            {
                AnnouncementText = LocalizedString("TextSavedChangesCopyAccessibility")
            });
        }

        private async void RefreshSimilarPagesForCrop()
        {
            LogService?.Log.Information("RefreshSimilarPagesForCrop");
            SimilarPageIndicesForCrop = null;

            // get indices
            List<int> indices = new List<int>();
            List<ScanResultElement> pages = new List<ScanResultElement>();
            List<BitmapImage> images = await ScanResult.GetImagesAsync();
            for (int i = 0; i < images.Count; i++)
            {
                if (i == SelectedPageIndex) continue;
                
                if (images[i].PixelWidth == SelectedPage.CachedImage.PixelWidth
                    && images[i].PixelHeight == SelectedPage.CachedImage.PixelHeight)
                {
                    // image has exact same dimensions as the currently selected one
                    indices.Add(i);
                    pages.Add(ScanResult.Elements[i]);
                }
            }

            SimilarPagesForCrop = pages;
            SimilarPageIndicesForCrop = indices;
        }

        private List<int> GetSelectedIndicesCropSimilarPages()
        {
            List<int> indices = new List<int>();

            foreach (ItemIndexRange range in SelectedRangesCropSimilarPages)
            {
                for (int i = range.FirstIndex; i <= range.LastIndex; i++)
                {
                    indices.Add(SimilarPageIndicesForCrop[i]);
                }
            }

            LogService?.Log.Information($"GetSelectedIndicesCropSimilarPages: Indices are {indices}");
            return indices;
        }
    }

    public enum EditorMode
    {
        Initial = 0,
        Crop = 1,
        Draw = 2,
    }

    public class OpenWithApp
    {
        public AppInfo AppInfo;
        public BitmapImage Logo;
    }
}
