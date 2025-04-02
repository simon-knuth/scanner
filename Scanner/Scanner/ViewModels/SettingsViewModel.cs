using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Scanner.Models;
using Scanner.Services;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Scanner.ViewModels
{
    public partial class SettingsViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        public readonly ISaveLocationService SaveLocationService = Ioc.Default.GetRequiredService<ISaveLocationService>();
        private readonly ISentryService? SentryService = Ioc.Default.GetService<ISentryService>();
        public readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        #region Commands
        public AsyncRelayCommand SelectFixedSaveLocationAsyncCommand => new AsyncRelayCommand(SelectFixedSaveLocationAsync);
        public AsyncRelayCommand ResetFixedSaveLocationAsyncCommand => new AsyncRelayCommand(ResetFixedSaveLocationAsync);
        public AsyncRelayCommand GenerateLogsListAsyncCommand => new AsyncRelayCommand(GenerateLogsListAsync);
        public AsyncRelayCommand<LogFile> ExportLogAsyncCommand => new AsyncRelayCommand<LogFile>(ExportLogAsync);
        public RelayCommand<DispatcherQueue> ViewLoadingCommand => new RelayCommand<DispatcherQueue>(ViewLoading);
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        public SettingsPage[] HeaderSettingsPages =
        [
            new SettingsPage(SettingsPageType.General, "\uE713", "General"),
            new SettingsPage(SettingsPageType.Personalization, "\uE771", "Personalization"),
            new SettingsPage(SettingsPageType.Privacy, "\uEA18", "Privacy"),
        ];

        public SettingsPage[] FooterSettingsPages =
        [
            new SettingsPage(SettingsPageType.Feedback, "\uED15", "Feedback"),
            new SettingsPage(SettingsPageType.About, "\uE946", "About"),
        ];

        [ObservableProperty]
        private SettingsPage selectedPage;

        [ObservableProperty]
        private List<LogFile>? logs;

        [ObservableProperty]
        private bool isFixedSaveLocationSupported;

        [ObservableProperty]
        private string? fixedSaveLocationPath;

        public int SettingSaveLocationType
        {
            get => (int)SettingsService.SettingSaveLocationType;
            set => SettingsService.SettingSaveLocationType = (SettingSaveLocationType)value;
        }

        public int SettingScanAction
        {
            get => (int)SettingsService.SettingScanAction;
            set => SettingsService.SettingScanAction = (SettingScanAction)value;
        }

        public int SettingAppTheme
        {
            get => (int)SettingsService.SettingAppTheme;
            set => SettingsService.SettingAppTheme = (SettingAppTheme)value;
        }

        public int SettingMeasurementUnits
        {
            get => (int)SettingsService.SettingMeasurementUnits;
            set => SettingsService.SettingMeasurementUnits = (SettingMeasurementUnits)value;
        }

        public int SettingEditorOrientation
        {
            get => (int)SettingsService.SettingEditorOrientation;
            set => SettingsService.SettingEditorOrientation = (SettingEditorOrientation)value;
        }

        public string CurrentVersion => Helpers.Helpers.GetCurrentVersion();

        private DispatcherQueue? viewDispatcherQueue;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SettingsViewModel()
        {
            SelectedPage = HeaderSettingsPages[0];
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }

        private async void ViewLoading(DispatcherQueue? dispatcherQueue)
        {
            viewDispatcherQueue = dispatcherQueue;
            IsFixedSaveLocationSupported = await SaveLocationService.GetIsFixedSaveLocationSupportedAsync();
            await UpdateFixedSaveLocationPath();
        }

        private async Task GenerateLogsListAsync()
        {
            // search for logs in folder
            if (LogService != null)
            {
                ILogService _logService = LogService;
                List<LogFile> result = await _logService.GetLogFilesAsync();
                Logs = result;
            }
        }

        private async Task ExportLogAsync(LogFile logFile)
        {
            try
            {
                // get file
                StorageFile file = logFile.File;

                // prepare picker
                var savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
#if DEBUG
                savePicker.FileTypeChoices.Add("JSON", new List<string>() { ".json" });
#else
                savePicker.FileTypeChoices.Add("TXT", new List<string>() { ".txt" });
#endif
                savePicker.SuggestedFileName = file.DisplayName;

                var hwnd = WindowNative.GetWindowHandle(((App)Application.Current).SettingsWindow);
                InitializeWithWindow.Initialize(savePicker, hwnd);

                // ask for location
                StorageFile targetFile = await savePicker.PickSaveFileAsync();

                // export
                if (targetFile != null)
                {
                    CachedFileManager.DeferUpdates(targetFile);

                    // write to file
                    await file.CopyAndReplaceAsync(targetFile);
                    await CachedFileManager.CompleteUpdatesAsync(targetFile);
                }
            }
            catch (Exception exc)
            {
                LogService?.Log.Warning(exc, "SettingsViewModel - Log export failed");
                SentryService?.TrackError(exc);
            }
        }

        private async Task SelectFixedSaveLocationAsync()
        {
            if (viewDispatcherQueue == null) return;
            await SaveLocationService.SelectFixedSaveLocationAsync(viewDispatcherQueue, ((App)Application.Current).SettingsWindow!);
            await UpdateFixedSaveLocationPath();
        }

        private async Task ResetFixedSaveLocationAsync()
        {
            await SaveLocationService.TryResetSaveLocationAsync();
            await UpdateFixedSaveLocationPath();
        }

        private async Task UpdateFixedSaveLocationPath()
        {
            FixedSaveLocationPath = (await SaveLocationService.GetFixedSaveLocationAsync())?.Path;
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public record SettingsPage(SettingsPageType PageType, string Glyph, string FriendlyName);

    public enum SettingsPageType
    {
        General,
        Personalization,
        Privacy,
        Feedback,
        About
    }
}
