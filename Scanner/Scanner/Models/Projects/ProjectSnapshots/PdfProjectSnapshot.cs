using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Storage;
using CommunityToolkit.Mvvm.DependencyInjection;
using Scanner.Services.Interfaces;
using Scanner.Models.Interfaces;
using System.ComponentModel;
using Windows.Graphics.Imaging;
using System.IO;
using Microsoft.UI.Dispatching;

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
        public Dictionary<IProjectPage, StorageFile> Pages { get; private set; } = new();


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
                Pages.Add(page, page.SourceFile);
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<Dictionary<IProjectPage, StorageFile?>> TrySaveAsync(DispatcherQueue uiDispatcherQueue)
        {
            Dictionary<IProjectPage, StorageFile?> result = new();

            // generate PDF
            try
            {
                TesseractService.GeneratePdf(Pages.Values.ToList(), Path.Combine(AppDataService.PdfOutputFolder.Path, tesseractOutputFileDisplayName));
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
    }
}
