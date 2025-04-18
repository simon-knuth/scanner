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

        public TargetFormat Format { get; private set; }

        public string? FileName { get; private set; }
        public StorageFolder TargetFolder { get; private set; }
        public StorageFile? TargetFile { get; private set; }

        public List<StorageFile> Pages { get; private set; } = new();


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PdfProjectSnapshot(Project project)
        {
            Format = project.Format;
            FileName = project.TargetFileName;
            TargetFolder = project.TargetFolder;
            TargetFile = project.TargetFile;

            foreach (IProjectPage page in project.Pages)
            {
                Pages.Add(page.SourceFile);
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<bool> TrySaveAsync()
        {
            // generate PDF
            try
            {
                TesseractService.GeneratePdf(Pages, Path.Combine(AppDataService.PdfOutputFolder.Path, "output"));
            }
            catch (Exception exc)
            {
                LogService?.Log.Error("PdfProjectSnapshot - Failed to generate PDF");
                return false;
            }

            // save PDF to target folder
            try
            {
                StorageFile pdfFile = await AppDataService.PdfOutputFolder.GetFileAsync("output.pdf");
                if (TargetFile != null)
                {
                    await pdfFile.MoveAndReplaceAsync(TargetFile);
                }
                else
                {
                    await pdfFile.MoveAsync(TargetFolder, FileName, NameCollisionOption.GenerateUniqueName);
                }
            }
            catch (Exception exc)
            {
                LogService?.Log.Error("PdfProjectSnapshot - Failed to save PDF to target folder");
                return false;
            }

            return true;
        }
    }
}
