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
using System.Collections.ObjectModel;
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
        private static readonly IOcrService OcrService = Ioc.Default.GetRequiredService<IOcrService>();
        private static readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        #endregion

        public TargetFormat Format { get; private set; } = TargetFormat.PDF;

        public string? DesiredFileName { get; private set; }
        public StorageFolder? TargetFolder { get; private set; }
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
            DesiredFileName = project.FileNameInfo!.DesiredName;
            TargetFolder = project.TargetFolder!;
            TargetFile = project.TargetFile;

            foreach (IProjectPage page in project.Pages)
            {
                if (page is ImagePage imagePage)
                {
                    Pages.Add(page, new(page.SourceFile, imagePage.Filter, imagePage.Brightness, imagePage.Contrast));
                }
                else
                {
                    Pages.Add(page, new(page.SourceFile, ImageFilter.None, 0, 0));
                }
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public Task<Dictionary<IProjectPage, StorageFile?>> TrySaveAsync(DispatcherQueue uiDispatcherQueue)
        {
            Dictionary<IProjectPage, IProjectSnapshotPage> pdfPages = Pages.ToDictionary(
                x => x.Key,
                x => (IProjectSnapshotPage)x.Value);
            return PdfProject.CreatePdfFromPagesAsync(pdfPages, TargetFile, DesiredFileName, TargetFolder, SettingsService.SettingOcrPdfs, uiDispatcherQueue);
        }
    }
}
