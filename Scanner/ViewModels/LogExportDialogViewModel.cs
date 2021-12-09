using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Scanner.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.ViewModels
{
    public class LogExportDialogViewModel : ObservableObject
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly ILogService LogService = Ioc.Default.GetRequiredService<ILogService>();
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();

        public AsyncRelayCommand ViewLoadedCommand;
        public AsyncRelayCommand<StorageFile> LogExportCommand;

        private List<Models.LogFile> _LogFiles;
        public List<Models.LogFile> LogFiles
        {
            get => _LogFiles;
            set => SetProperty(ref _LogFiles, value);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public LogExportDialogViewModel()
        {
            ViewLoadedCommand = new AsyncRelayCommand(LoadLogFilesAsync);
            LogExportCommand = new AsyncRelayCommand<StorageFile>(LogExportAsync);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Requests the available <see cref="LogFile"/>s from <see cref="LogService"/> and
        ///     fills <see cref="LogFiles"/> with them.
        /// </summary>
        private async Task LoadLogFilesAsync()
        {
            LogFiles = await LogService.GetLogFiles();
        }

        /// <summary>
        ///     Exports the given <paramref name="sourceFile"/> to a location that the user selects.
        /// </summary>
        private async Task LogExportAsync(StorageFile sourceFile)
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
#if DEBUG
            savePicker.FileTypeChoices.Add("JSON", new List<string>() { ".json" });
#else
            savePicker.FileTypeChoices.Add("TXT", new List<string>() { ".txt" });
#endif
            savePicker.SuggestedFileName = sourceFile.DisplayName;

            StorageFile targetFile = await savePicker.PickSaveFileAsync();
            if (targetFile != null)
            {
                CachedFileManager.DeferUpdates(targetFile);

                // write to file
                await sourceFile.CopyAndReplaceAsync(targetFile);
                await CachedFileManager.CompleteUpdatesAsync(targetFile);
            }
        }
    }
}
