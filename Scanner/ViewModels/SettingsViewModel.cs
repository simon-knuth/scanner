using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using static Utilities;

namespace Scanner.ViewModels
{
    public class SettingsViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        private readonly IScanService ScanService = Ioc.Default.GetRequiredService<IScanService>();
        private readonly IHelperService HelperService = Ioc.Default.GetRequiredService<IHelperService>();
        public readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        public readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        public readonly IAutoRotatorService AutoRotatorService = Ioc.Default.GetService<IAutoRotatorService>();

        public RelayCommand DisposeCommand;
        public RelayCommand DisplayLogExportDialogCommand;
        public RelayCommand DisplayLicensesDialogCommand;
        public AsyncRelayCommand StoreRatingCommand;
        public AsyncRelayCommand ChooseSaveLocationCommand;
        public AsyncRelayCommand ResetSaveLocationCommand;
        public RelayCommand DisplayChangelogCommand;
        public RelayCommand ShowDonateDialogCommand;
        public RelayCommand<string> SetAutoRotateLanguageCommand;
        public AsyncRelayCommand LaunchLanguageSettingsCommand;

        public event EventHandler ChangelogRequested;

        private string _SaveLocationPath;
        public string SaveLocationPath
        {
            get => _SaveLocationPath;
            set => SetProperty(ref _SaveLocationPath, value);
        }

        private bool? _IsDefaultSaveLocation;
        public bool? IsDefaultSaveLocation
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

        public bool SettingAutoRotate
        {
            get => (bool)SettingsService.GetSetting(AppSetting.SettingAutoRotate);
            set => SettingsService.SetSetting(AppSetting.SettingAutoRotate, value);
        }

        public string SettingAutoRotateLanguage
        {
            get => (string)SettingsService.GetSetting(AppSetting.SettingAutoRotateLanguage);
            set => SettingsService.SetSetting(AppSetting.SettingAutoRotateLanguage, value);
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

        public bool SettingShowSurveys
        {
            get => (bool)SettingsService.GetSetting(AppSetting.SettingShowSurveys);
            set => SettingsService.SetSetting(AppSetting.SettingShowSurveys, value);
        }

        private bool _IsScanInProgress;
        public bool IsScanInProgress
        {
            get => _IsScanInProgress;
            set => SetProperty(ref _IsScanInProgress, value);
        }

        public string CurrentVersion => GetCurrentVersion();

        public event EventHandler LogExportDialogRequested;
        public event EventHandler LicensesDialogRequested;

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
            DisplayLicensesDialogCommand = new RelayCommand(DisplayLicensesDialog);
            DisplayChangelogCommand = new RelayCommand(DisplayChangelog);
            StoreRatingCommand = new AsyncRelayCommand(DisplayStoreRatingDialogAsync);
            ChooseSaveLocationCommand = new AsyncRelayCommand(ChooseSaveLocation);
            ResetSaveLocationCommand = new AsyncRelayCommand(ResetSaveLocationAsync);
            ShowDonateDialogCommand = new RelayCommand(() => Messenger.Send(new DonateDialogRequestMessage()));
            SetAutoRotateLanguageCommand = new RelayCommand<string>((x) => SetAutoRotateLanguage(int.Parse(x)));
            LaunchLanguageSettingsCommand = new AsyncRelayCommand(LaunchLanguageSettings);
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

        private void DisplayLicensesDialog()
        {
            LicensesDialogRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task DisplayStoreRatingDialogAsync()
        {
            await RunOnUIThreadAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await HelperService.ShowRatingDialogAsync();
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
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessagePickFolderHeading"),
                    MessageText = LocalizedString("ErrorMessagePickFolderBody"),
                    AdditionalText = exc.Message,
                    Severity = MessengerEnums.AppWideStatusMessageSeverity.Error
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
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageResetFolderUnauthorizedHeading"),
                    MessageText = LocalizedString("ErrorMessageResetFolderUnauthorizedBody"),
                    AdditionalText = exc.Message,
                    Severity = MessengerEnums.AppWideStatusMessageSeverity.Error
                });
                return;
            }
            catch (Exception exc)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageResetFolderHeading"),
                    MessageText = LocalizedString("ErrorMessageResetFolderBody"),
                    AdditionalText = exc.Message,
                    Severity = MessengerEnums.AppWideStatusMessageSeverity.Error
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

        private void DisplayChangelog()
        {
            ChangelogRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task LaunchLanguageSettings()
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:regionlanguage"));
        }

        private void SetAutoRotateLanguage(int language)
        {
            if (language < AutoRotatorService.AvailableLanguages.Count)
            {
                SettingAutoRotateLanguage = AutoRotatorService.AvailableLanguages[language].LanguageTag;
            }
        }
    }
}
