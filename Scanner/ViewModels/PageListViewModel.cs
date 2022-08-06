using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Helpers;
using Scanner.Services;
using Scanner.Services.Messenger;
using Scanner.Views.Converters;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml.Data;
using static Utilities;

namespace Scanner.ViewModels
{
    public class PageListViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        public readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        private readonly ILogService LogService = Ioc.Default.GetRequiredService<ILogService>();
        public readonly IScanResultService ScanResultService = Ioc.Default.GetRequiredService<IScanResultService>();
        public readonly IScanService ScanService = Ioc.Default.GetRequiredService<IScanService>();
        public readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        #region Commands
        public RelayCommand DisposeCommand;
        public AsyncRelayCommand<string> RotateCommand;
        public AsyncRelayCommand DeleteCommand;
        public AsyncRelayCommand CopyCommand;
        public RelayCommand ShareCommand;
        public AsyncRelayCommand<StorageFile> ShowInFileExplorerCommand;
        public AsyncRelayCommand<ScanResultElement> DuplicatePageCommand;
        public AsyncRelayCommand DragDropPage;
        #endregion

        #region Events
        public event EventHandler TargetedShareUiRequested;
        public event EventHandler RotateSuccessful;
        public event EventHandler DeleteSuccessful;
        public event EventHandler CopySuccessful;
        #endregion

        private ScanResult _ScanResult;
        public ScanResult ScanResult
        {
            get => _ScanResult;
            set => SetProperty(ref _ScanResult, value);
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
                    if (value != -1) Messenger.Send(new PageListCurrentIndexChangedMessage(value));
                }
            }
        }

        private IReadOnlyList<ItemIndexRange> _SelectedRanges;
        public IReadOnlyList<ItemIndexRange> SelectedRanges
        {
            get => _SelectedRanges;
            set => SetProperty(ref _SelectedRanges, value);
        }

        private bool _IsScanRunning;
        public bool IsScanRunning
        {
            get => _IsScanRunning;
            set => SetProperty(ref _IsScanRunning, value);
        }

        private bool _IsScanResultChanging;
        public bool IsScanResultChanging
        {
            get => _IsScanResultChanging;
            set => SetProperty(ref _IsScanResultChanging, value);
        }

        private bool _IsEditorEditing;
        public bool IsEditorEditing
        {
            get => _IsEditorEditing;
            set => SetProperty(ref _IsEditorEditing, value);
        }

        private bool _DisplayContainingFolders;
        public bool DisplayContainingFolders
        {
            get => _DisplayContainingFolders;
            set => SetProperty(ref _DisplayContainingFolders, value);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PageListViewModel()
        {
            ScanResult = ScanResultService.Result;
            ScanResultService.ScanResultCreated += ScanResultService_ScanResultCreated;
            ScanResultService.ScanResultDismissed += ScanResultService_ScanResultDismissed;
            IsScanResultChanging = ScanResultService.IsScanResultChanging;
            ScanResultService.ScanResultChanging += ScanResultService_ScanResultChanging;
            ScanResultService.ScanResultChanged += ScanResultService_ScanResultChanged;
            IsScanRunning = ScanService.IsScanInProgress;
            ScanService.ScanStarted += ScanService_ScanStarted;
            ScanService.ScanEnded += ScanService_ScanEnded;
            SettingsService.SettingChanged += SettingsService_SettingChanged;
            DisplayContainingFolders = (SettingSaveLocationType)SettingsService.GetSetting(AppSetting.SettingSaveLocationType)
                == SettingSaveLocationType.AskEveryTime;

            DisposeCommand = new RelayCommand(Dispose);
            RotateCommand = new AsyncRelayCommand<string>((x) => RotateAsync((BitmapRotation)int.Parse(x)));
            DeleteCommand = new AsyncRelayCommand(DeleteAsync);
            CopyCommand = new AsyncRelayCommand(CopyAsync);
            ShareCommand = new RelayCommand(Share);
            DragDropPage = new AsyncRelayCommand(async () => await ScanResultService.ApplyElementOrderToFilesAsync()); ;
            ShowInFileExplorerCommand = new AsyncRelayCommand<StorageFile>((x) => ShowFileInFileExplorerAsync(x));
            DuplicatePageCommand = new AsyncRelayCommand<ScanResultElement>((x) => DuplicatePageAsync(x));

            Messenger.Register<EditorCurrentIndexChangedMessage>(this, (r, m) => SelectedPageIndex = m.Value);
            SelectedPageIndex = Messenger.Send(new EditorCurrentIndexRequestMessage());

            Messenger.Register<EditorIsEditingChangedMessage>(this, (r, m) => IsEditorEditing = m.Value);
            IsEditorEditing = Messenger.Send(new EditorIsEditingRequestMessage());

            if (ScanResultService.Result != null)
            {
                DisplayContainingFolders = (SettingSaveLocationType)SettingsService.GetSetting(AppSetting.SettingSaveLocationType)
                    == SettingSaveLocationType.AskEveryTime
                    && IsImageFormat(ScanResultService.Result.ScanResultFormat);
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            // clean up messenger
            Messenger.UnregisterAll(this);

            // clean up event handlers
            ScanResultService.ScanResultCreated -= ScanResultService_ScanResultCreated;
            ScanResultService.ScanResultDismissed -= ScanResultService_ScanResultDismissed;
            ScanResultService.ScanResultChanging -= ScanResultService_ScanResultChanging;
            ScanResultService.ScanResultChanged -= ScanResultService_ScanResultChanged;
            ScanService.ScanStarted -= ScanService_ScanStarted;
            ScanService.ScanEnded -= ScanService_ScanEnded;
            SettingsService.SettingChanged -= SettingsService_SettingChanged;
        }

        private void SettingsService_SettingChanged(object sender, AppSetting e)
        {
            if (e == AppSetting.SettingSaveLocationType && ScanResultService.Result != null)
            {
                DisplayContainingFolders = (SettingSaveLocationType)SettingsService.GetSetting(AppSetting.SettingSaveLocationType)
                    == SettingSaveLocationType.AskEveryTime
                    && IsImageFormat(ScanResultService.Result.ScanResultFormat);
            }
        }

        private void ScanResultService_ScanResultDismissed(object sender, EventArgs e)
        {
            ScanResult = null;
        }

        private void ScanResultService_ScanResultCreated(object sender, ScanResult e)
        {
            ScanResult = e;
            SelectedPageIndex = Messenger.Send(new EditorCurrentIndexRequestMessage());
            DisplayContainingFolders = (SettingSaveLocationType)SettingsService.GetSetting(AppSetting.SettingSaveLocationType)
                    == SettingSaveLocationType.AskEveryTime
                    && IsImageFormat(e.ScanResultFormat);
        }

        private void ScanResultService_ScanResultChanged(object sender, EventArgs e)
        {
            IsScanResultChanging = false;
        }

        private void ScanResultService_ScanResultChanging(object sender, EventArgs e)
        {
            IsScanResultChanging = true;
        }

        private void ScanService_ScanEnded(object sender, EventArgs e)
        {
            IsScanRunning = false;
        }

        private void ScanService_ScanStarted(object sender, ScanAndEditingProgress e)
        {
            IsScanRunning = true;
        }

        private List<int> GetSelectedIndices()
        {
            LogService?.Log.Information("GetSelectedIndices");
            List<int> indices = new List<int>();
            
            foreach (ItemIndexRange range in SelectedRanges)
            {
                for (int i = range.FirstIndex; i <= range.LastIndex; i++)
                {
                    indices.Add(i);
                }
            }

            LogService?.Log.Information($"GetSelectedIndices: {indices}");
            return indices;
        }

        private async Task RotateAsync(BitmapRotation rotation)
        {
            // collect files
            List<StorageFile> list = new List<StorageFile>();
            List<int> indices = GetSelectedIndices();

            // create instructions
            List<Tuple<int, BitmapRotation>> instructions = new List<Tuple<int, BitmapRotation>>();
            foreach (int index in indices)
            {
                instructions.Add(new Tuple<int, BitmapRotation>(index, rotation));
            }

            // rotate
            bool success = await ScanResultService.RotatePagesAsync(instructions);
            if (!success) return;

            RotateSuccessful?.Invoke(this, EventArgs.Empty);
            Messenger.Send(new NarratorAnnouncementMessage
            {
                AnnouncementText = LocalizedString("TextSavedChangesAccessibility")
            });
        }
        private async Task DeleteAsync()
        {
            // collect files
            List<StorageFile> list = new List<StorageFile>();
            List<int> indices = GetSelectedIndices();

            // delete
            bool success;
            success = await ScanResultService.DeleteScansAsync(indices);
            if (!success) return;

            DeleteSuccessful?.Invoke(this, EventArgs.Empty);
            Messenger.Send(new NarratorAnnouncementMessage
            {
                AnnouncementText = LocalizedString("TextSavedChangesAccessibility")
            });
        }

        private async Task CopyAsync()
        {
            // collect files
            List<StorageFile> list = new List<StorageFile>();
            List<int> indices = GetSelectedIndices();

            // copy
            bool success;
            if (ScanResult.NumberOfPages == indices.Count
                && ScanResult.IsImage == false)
            {
                // entire PDF document selected ~> copy as PDF
                success = await ScanResultService.CopyAsync();
            }
            else
            {
                // copy selected images
                success = await ScanResultService.CopyImagesAsync(indices);
            }
            if (!success) return;

            CopySuccessful?.Invoke(this, EventArgs.Empty);
        }

        private void Share()
        {
            // collect files
            List<StorageFile> list = new List<StorageFile>();
            List<int> indices = GetSelectedIndices();

            if (ScanResult.NumberOfPages == indices.Count
                && ScanResult.IsImage == false)
            {
                // entire PDF document selected ~> share as PDF
                list.Add(ScanResult.Pdf);
            }
            else
            {
                // share selected images
                foreach (int index in indices)
                {
                    list.Add(ScanResult.GetImageFile(index));
                }
            }

            // request share UI
            Messenger.Send(new SetShareFilesMessage { Files = list });
            TargetedShareUiRequested?.Invoke(this, EventArgs.Empty);

            AppCenterService?.TrackEvent(AppCenterEvent.Share);
        }

        private async Task ShowFileInFileExplorerAsync(StorageFile file)
        {
            LogService?.Log.Information("ShowFileInFileExplorerAsync");
            try
            {
                FilePathFolderPathConverter converter = new FilePathFolderPathConverter();
                string folderPath = (string)converter.Convert(file.Path, null, null, null);

                FolderLauncherOptions options = new FolderLauncherOptions();
                options.ItemsToSelect.Add(file);
                await Launcher.LaunchFolderPathAsync(folderPath, options);
            }
            catch (Exception) { }
        }

        private async Task DuplicatePageAsync(ScanResultElement element)
        {
            LogService?.Log.Information("DuplicatePageAsync");
            int index = ScanResult.Elements.IndexOf(element);
            bool success = await ScanResultService.DuplicatePageAsync(index);

            if (!success) return;
            Messenger.Send(new NarratorAnnouncementMessage
            {
                AnnouncementText = LocalizedString("TextSavedChangesAccessibility")
            });
        }
    }
}
