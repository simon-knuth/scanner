using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using static Scanner.Services.SettingsEnums;
using static Utilities;

namespace Scanner.ViewModels
{
    public class SettingsViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        private readonly IScanService ScanService = Ioc.Default.GetRequiredService<IScanService>();
        public readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        public readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

        public RelayCommand DisposeCommand;
        public RelayCommand DisplayLogExportDialogCommand;
        public AsyncRelayCommand StoreRatingCommand;
        public AsyncRelayCommand ChooseSaveLocationCommand;
        public AsyncRelayCommand ResetSaveLocationCommand;

        private string _SaveLocationPath;
        public string SaveLocationPath
        {
            get => _SaveLocationPath;
            set => SetProperty(ref _SaveLocationPath, value);
        }

        private bool _IsDefaultSaveLocation;
        public bool IsDefaultSaveLocation
        {
            get => _IsDefaultSaveLocation;
            set => SetProperty(ref _IsDefaultSaveLocation, value);
        }

        public int SettingSaveLocationType
        {
            get => (int)SettingsService.GetSetting(AppSetting.SettingSaveLocationType);
            set => SettingsService.SetSetting(AppSetting.SettingSaveLocationType, value);
        }

        public int SettingAppTheme
        {
            get => (int)SettingsService.GetSetting(AppSetting.SettingAppTheme);
            set => SettingsService.SetSetting(AppSetting.SettingAppTheme, value);
        }

        public int SettingAutoRotate
        {
            get => (int)SettingsService.GetSetting(AppSetting.SettingAutoRotate);
            set => SettingsService.SetSetting(AppSetting.SettingAutoRotate, value);
        }

        public bool SettingAppendTime
        {
            get => (bool)SettingsService.GetSetting(AppSetting.SettingAppendTime);
            set => SettingsService.SetSetting(AppSetting.SettingAppendTime, value);
        }

        public int SettingEditorOrientation
        {
            get => (int)SettingsService.GetSetting(AppSetting.SettingEditorOrientation);
            set => SettingsService.SetSetting(AppSetting.SettingEditorOrientation, value);
        }

        public bool SettingRememberScanOptions
        {
            get => (bool)SettingsService.GetSetting(AppSetting.SettingRememberScanOptions);
            set => SettingsService.SetSetting(AppSetting.SettingRememberScanOptions, value);
        }

        public bool SettingErrorStatistics
        {
            get => (bool)SettingsService.GetSetting(AppSetting.SettingErrorStatistics);
            set => SettingsService.SetSetting(AppSetting.SettingErrorStatistics, value);
        }

        private bool _IsScanInProgress;
        public bool IsScanInProgress
        {
            get => _IsScanInProgress;
            set => SetProperty(ref _IsScanInProgress, value);
        }

        public string CurrentVersion => GetCurrentVersion();

        public event EventHandler LogExportDialogRequested;

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SettingsViewModel()
        {
            SettingsService.ScanSaveLocationChanged += SettingsService_ScanSaveLocationChanged;
            SaveLocationPath = SettingsService.ScanSaveLocation?.Path;
            IsDefaultSaveLocation = SettingsService.IsScanSaveLocationDefault;
            ScanService.ScanStarted += ScanService_ScanStartedOrCompleted;
            ScanService.ScanEnded += ScanService_ScanStartedOrCompleted;
            IsScanInProgress = ScanService.IsScanInProgress;

            DisposeCommand = new RelayCommand(Dispose);
            DisplayLogExportDialogCommand = new RelayCommand(DisplayLogExportDialog);
            StoreRatingCommand = new AsyncRelayCommand(DisplayStoreRatingDialogAsync);
            ChooseSaveLocationCommand = new AsyncRelayCommand(ChooseSaveLocation);
            ResetSaveLocationCommand = new AsyncRelayCommand(ResetSaveLocationAsync);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            // clean up messenger
            Messenger.UnregisterAll(this);

            // clean up event handlers
            SettingsService.ScanSaveLocationChanged -= SettingsService_ScanSaveLocationChanged;
            ScanService.ScanStarted -= ScanService_ScanStartedOrCompleted;
            ScanService.ScanEnded -= ScanService_ScanStartedOrCompleted;
        }

        private void DisplayLogExportDialog()
        {
            LogExportDialogRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task DisplayStoreRatingDialogAsync()
        {
            await RunOnUIThreadAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await ShowRatingDialogAsync();
            });
        }

        private async Task ChooseSaveLocation()
        {
            if (ChooseSaveLocationCommand.IsRunning) return;    // already running?

            // prepare folder picker
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            folderPicker.FileTypeFilter.Add("*");

            // pick folder and check it
            StorageFolder folder;
            try
            {
                folder = await folderPicker.PickSingleFolderAsync();
                if (folder == null) return;
            }
            catch (Exception exc)
            {
                LogService?.Log.Warning(exc, "Picking a new save location failed.");
                Messenger.Send(new AppWideMessage
                {
                    Title = LocalizedString("ErrorMessagePickFolderHeading"),
                    MessageText = LocalizedString("ErrorMessagePickFolderBody"),
                    AdditionalText = exc.Message,
                    Severity = MessengerEnums.AppWideMessageSeverity.Error
                });
                return;
            }

            // check same folder as before
            if (folder.Path == SettingsService.ScanSaveLocation.Path) return;

            await SettingsService.SetScanSaveLocationAsync(folder);

            LogService?.Log.Information("Successfully selected new save location.");
        }

        private async Task ResetSaveLocationAsync()
        {
            if (ResetSaveLocationCommand.IsRunning) return;     // already running?

            try
            {
                await SettingsService.ResetScanSaveLocationAsync();
            }
            catch (UnauthorizedAccessException exc)
            {
                Messenger.Send(new AppWideMessage
                {
                    Title = LocalizedString("ErrorMessageResetFolderUnauthorizedHeading"),
                    MessageText = LocalizedString("ErrorMessageResetFolderUnauthorizedBody"),
                    AdditionalText = exc.Message,
                    Severity = MessengerEnums.AppWideMessageSeverity.Error
                });
                return;
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideMessage
                {
                    Title = LocalizedString("ErrorMessageResetFolderHeading"),
                    MessageText = LocalizedString("ErrorMessageResetFolderBody"),
                    AdditionalText = exc.Message,
                    Severity = MessengerEnums.AppWideMessageSeverity.Error
                });
                return;
            }
        }

        private void SettingsService_ScanSaveLocationChanged(object sender, EventArgs e)
        {
            SaveLocationPath = SettingsService.ScanSaveLocation?.Path;
            IsDefaultSaveLocation = SettingsService.IsScanSaveLocationDefault;
        }

        private void ScanService_ScanStartedOrCompleted(object sender, EventArgs e)
        {
            IsScanInProgress = ScanService.IsScanInProgress;
        }
    }
}
