using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using static Utilities;

namespace Scanner.ViewModels
{
    public class SetupDialogViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        public readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private readonly ILogService LogService = Ioc.Default.GetRequiredService<ILogService>();
        public readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();
        #endregion

        #region Commands
        public RelayCommand ConfirmSettingsCommand;
        public AsyncRelayCommand ChooseSaveLocationCommand => new AsyncRelayCommand(ChooseSaveLocation);
        public AsyncRelayCommand ResetSaveLocationCommand => new AsyncRelayCommand(ResetSaveLocationAsync);
        #endregion

        #region Events
        public event EventHandler<Tuple<string, string>> ErrorOccurred;
        #endregion

        private bool _ProxySettingErrorStatistics = true;
        public bool ProxySettingErrorStatistics
        {
            get => _ProxySettingErrorStatistics;
            set
            {
                SetProperty(ref _ProxySettingErrorStatistics, value);
            }
        }

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


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SetupDialogViewModel()
        {
            ConfirmSettingsCommand = new RelayCommand(ConfirmSettings);
            SaveLocationPath = SettingsService.ScanSaveLocation?.Path;
            IsDefaultSaveLocation = SettingsService.IsScanSaveLocationDefault;
            SettingsService.ScanSaveLocationChanged += SettingsService_ScanSaveLocationChanged;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ConfirmSettings()
        {
            LogService?.Log.Information("ConfirmSettings");

            SettingsService.SetSetting(AppSetting.SettingErrorStatistics, ProxySettingErrorStatistics);
            SettingsService.SetSetting(AppSetting.SetupCompleted, true);

            Messenger.Send(new SetupCompletedMessage());
        }

        private void SettingsService_ScanSaveLocationChanged(object sender, System.EventArgs e)
        {
            SaveLocationPath = SettingsService.ScanSaveLocation?.Path;
            IsDefaultSaveLocation = SettingsService.IsScanSaveLocationDefault;
        }

        private async Task ChooseSaveLocation()
        {
            if (ChooseSaveLocationCommand.IsRunning) return;    // already running?
            LogService?.Log.Information("ChooseSaveLocation");

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
                ErrorOccurred?.Invoke(this, new Tuple<string, string>(LocalizedString("ErrorMessagePickFolderHeading"),
                    LocalizedString("ErrorMessagePickFolderBody")));
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
            LogService?.Log.Information("ResetSaveLocationAsync");

            try
            {
                await SettingsService.ResetScanSaveLocationAsync();
            }
            catch (UnauthorizedAccessException)
            {
                ErrorOccurred?.Invoke(this, new Tuple<string, string>(LocalizedString("ErrorMessageResetFolderUnauthorizedHeading"),
                    LocalizedString("ErrorMessageResetFolderUnauthorizedBody")));
                return;
            }
            catch (Exception)
            {
                ErrorOccurred?.Invoke(this, new Tuple<string, string>(LocalizedString("ErrorMessageResetFolderHeading"),
                    LocalizedString("ErrorMessageResetFolderBody")));
                return;
            }
        }
    }
}
