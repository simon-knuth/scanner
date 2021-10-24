using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Storage;

using static Globals;


namespace Scanner.Services
{
    /// <summary>
    ///     Manages the PDF conversion (kicks it off and processes the result).
    /// </summary>
    internal class PdfService : ObservableObject, IPdfService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        private IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private IHelperService HelperService = Ioc.Default.GetRequiredService<IHelperService>();

        public event EventHandler<bool> GenerationEnded;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PdfService()
        {
            
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<StorageFile> GeneratePdfAsync(string name, StorageFolder targetFolder)
        {
            taskCompletionSource = new TaskCompletionSource<bool>();

            LogService?.Log.Information("Requested PDF generation.");

            string newName;
            StorageFile newPdf;

            newPdf = await AppDataService.FolderTemp.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
            newName = newPdf.Name;

            try
            {
                taskCompletionSource = new TaskCompletionSource<bool>();
                var win32ResultAsync = taskCompletionSource.Task;

                // save the target name
                ApplicationData.Current.LocalSettings.Values["targetFileName"] = newPdf.Name;

                // delete potential rogue files
                await AppDataService.EmptyReceivedPagesFolderAsync();

                int attempt = 1;
                while (attempt >= 0)
                {
                    // call win32 app and wait for result
                    LogService?.Log.Information("Launching full trust process.");
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                    await win32ResultAsync.ConfigureAwait(false);
                    LogService?.Log.Information("Full trust process is done.");

                    // get result file and move it to its correct folder
                    try
                    {
                        newPdf = null;
                        newPdf = await AppDataService.FolderTemp.GetFileAsync(newName);
                        attempt = -1;
                    }
                    catch (Exception)
                    {
                        if (attempt == 3) throw;

                        attempt++;
                        await Task.Delay(TimeSpan.FromSeconds(3));
                    }
                }

                // move PDF file to target folder
                await HelperService.MoveFileToFolderAsync(newPdf, targetFolder, newName, true);

                return newPdf;
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Generating the PDF failed. Attempted to generate " + newName);
                var files = await AppDataService.FolderTemp.GetFilesAsync();
                LogService?.Log.Information("State of temp folder: {@Folder}", files.Select(f => f.Name).ToList());
                AppCenterService.TrackError(exc);
                throw;
            }
        }
    }
}
