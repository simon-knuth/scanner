using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace Scanner.Models
{
    public partial class PdfProjectSnapshot : IProjectSnapshot
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        private static readonly ITesseractService TesseractService = Ioc.Default.GetRequiredService<ITesseractService>();
        #endregion

        #region Constants
        private const string tesseractOutputFileDisplayName = "tessoutput";
        #endregion

        public TargetFormat Format { get; private set; }

        public string DesiredFileName { get; private set; }
        public StorageFolder TargetFolder { get; private set; }
        public StorageFile? TargetFile { get; private set; }

        /// <remarks>
        /// Editing the <see cref="IProjectPage"/> references from the snapshot is not allowed.
        /// </remarks>
        public Dictionary<IProjectPage, PdfProjectSnapshotPage> Pages { get; private set; } = new();


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PdfProjectSnapshot(PdfProject project)
        {
            Format = project.Format;
            DesiredFileName = project.FileNameInfo!.DesiredName;
            TargetFolder = project.TargetFolder!;
            TargetFile = project.TargetFile;

            foreach (IProjectPage page in project.Pages)
            {
                if (page is ImagePage imagePage)
                {
                    Pages.Add(page, new(page.SourceFile, imagePage.Filter));
                }
                else
                {
                    Pages.Add(page, new(page.SourceFile, ImageFilter.None));
                }
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<Dictionary<IProjectPage, StorageFile?>> TrySaveAsync(DispatcherQueue uiDispatcherQueue)
        {
            Dictionary<IProjectPage, StorageFile?> result = new();
            List<StorageFile> files = [];

            // generate PDF
            try
            {
                await TesseractService.GeneratePdfAsync(Pages.Values.ToList(), Path.Combine(AppDataService.PdfOutputFolder.Path, tesseractOutputFileDisplayName), uiDispatcherQueue);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "PdfProjectSnapshot - Failed to generate PDF");
                return result;
            }

            // save PDF to target folder
            try
            {
                StorageFile generatedFile = await AppDataService.PdfOutputFolder.GetFileAsync($"{tesseractOutputFileDisplayName}.pdf");
                if (TargetFile != null)
                {
                    await generatedFile.MoveAndReplaceAsync(TargetFile);

                    if (generatedFile.Name != DesiredFileName)
                    {
                        await generatedFile.RenameAsync(DesiredFileName, NameCollisionOption.GenerateUniqueName);
                    }
                }
                else
                {
                    await generatedFile.MoveAsync(TargetFolder, DesiredFileName, NameCollisionOption.GenerateUniqueName);
                }
                result.Add(Pages.Keys.First(), generatedFile);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "PdfProjectSnapshot - Failed to save PDF to target folder");
            }

            return result;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public record PdfProjectSnapshotPage(StorageFile SourceFile, ImageFilter Filter);
    }
}
