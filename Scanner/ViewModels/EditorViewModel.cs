using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Scanners;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using static Scanner.Services.SettingsEnums;
using static Utilities;

namespace Scanner.ViewModels
{
    public class EditorViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetRequiredService<IAppCenterService>();
        private readonly ILogService LogService = Ioc.Default.GetRequiredService<ILogService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        public readonly IScanService ScanService = Ioc.Default.GetRequiredService<IScanService>();
        public readonly IScanResultService ScanResultService = Ioc.Default.GetRequiredService<IScanResultService>();

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

        public event EventHandler CropSuccessful;
        public event EventHandler RotateSuccessful;
        public event EventHandler DrawSuccessful;
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
                if (value == false) RefreshSelectedPageText();
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

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public EditorViewModel()
        {
            RefreshOrientationSetting();
            SettingsService.SettingChanged += SettingsService_SettingChanged;

            ScanResultService.ScanResultCreated += ScanResultService_ScanResultCreated;
            ScanResultService.ScanResultDismissed += ScanResultService_ScanResultDismissed;
            IsScanResultChanging = ScanResultService.IsScanResultChanging;
            ScanResultService.ScanResultChanging += (x, y) => IsScanResultChanging = true;
            ScanResultService.ScanResultChanged += (x, y) => IsScanResultChanging = false;

            IsScanning = ScanService.IsScanInProgress;
            ScanService.ScanStarted += (x, y) => IsScanning = true;
            ScanService.ScanEnded += (x, y) => IsScanning = false;

            Messenger.Register<EditorCurrentIndexRequestMessage>(this, (r, m) => m.Reply(SelectedPageIndex));
            Messenger.Register<PageListCurrentIndexChangedMessage>(this, (r, m) => SelectedPageIndex = m.Value);
            Messenger.Register<EditorIsEditingRequestMessage>(this, (r, m) => m.Reply(IsEditing));

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

        private void SettingsService_SettingChanged(object sender, AppSetting e)
        {
            if (e == AppSetting.SettingEditorOrientation)
            {
                RefreshOrientationSetting();
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

            Messenger.Send(new EditorSelectionTitleChangedMessage
            {
                Title = SelectedPage?.ScanFile?.Name
            });
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
        }

        private void EnterCropMode()
        {
            EditorMode = EditorMode.Crop;
        }

        private void LeaveCropMode()
        {
            EditorMode = EditorMode.Initial;
        }

        private void EnterDrawMode()
        {
            EditorMode = EditorMode.Draw;
        }

        private void LeaveDrawMode()
        {
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

            if (ScanResultService.Result.IsImage)
            {
                // open single image file
                if (index == -1) await ScanResultService.OpenImageWithAsync(SelectedPageIndex);
                else await ScanResultService.OpenImageWithAsync(SelectedPageIndex, OpenWithApps[index].AppInfo);
            }
            else
            {
                // open PDF document
                if (index == -1) await ScanResultService.OpenWithAsync();
                else await ScanResultService.OpenWithAsync(OpenWithApps[index].AppInfo);
            }
        }

        private async Task DeleteAsync()
        {
            bool success;

            success = await ScanResultService.DeleteScanAsync(SelectedPageIndex);

            if (!success) return;

            DeleteSuccessful?.Invoke(this, EventArgs.Empty);
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
                RandomAccessStreamReference stream = appInfo.DisplayInfo.GetLogo(new Size(64, 64));
                IRandomAccessStreamWithContentType content = await stream.OpenReadAsync();

                BitmapImage bmp = new BitmapImage();
                await bmp.SetSourceAsync(content);

                result.Add(new OpenWithApp
                {
                    AppInfo = appInfo,
                    Logo = bmp
                });

                if (result.Count >= 4) break;   // 4 apps max
            }

            return result;
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
