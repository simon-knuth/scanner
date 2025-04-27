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

        public string? DesiredFileName { get; private set; }
        public StorageFolder TargetFolder { get; private set; }
        public StorageFile? TargetFile { get; private set; }

        public List<StorageFile> Pages { get; private set; } = new();


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PdfProjectSnapshot(Project project)
        {
            Format = project.Format;
            DesiredFileName = project.FileNameInfo!.DesiredName;
            TargetFolder = project.TargetFolder!;
            TargetFile = project.TargetFile;

            foreach (IProjectPage page in project.Pages)
            {
                Pages.Add(page.SourceFile);
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<(bool, StorageFile?)> TrySaveAsync()
        {
            // generate PDF
            try
            {
                TesseractService.GeneratePdf(Pages, Path.Combine(AppDataService.PdfOutputFolder.Path, tesseractOutputFileDisplayName));
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "PdfProjectSnapshot - Failed to generate PDF");
                return (false, null);
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
                return (true, generatedFile);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "PdfProjectSnapshot - Failed to save PDF to target folder");
                return (false, null);
            }
        }
    }
}
