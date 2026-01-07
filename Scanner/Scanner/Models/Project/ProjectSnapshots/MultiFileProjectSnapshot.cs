using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Scanner.Extensions;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models
{
    public partial class MultiFileProjectSnapshot : IProjectSnapshot
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

        #region Constants
        private const double jpegQuality = 0.85;
        #endregion

        public TargetFormat Format { get; private set; }

        /// <remarks>
        /// Editing the <see cref="IProjectPage"/> references from the snapshot is not allowed.
        /// </remarks>
        public Dictionary<IProjectPage, MultiFileProjectSnapshotPage> Pages { get; private set; } = new();


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public MultiFileProjectSnapshot(MultiFileProject project)
        {
            Format = project.Format;

            foreach (IProjectPage page in project.Pages)
            {
                if (page is ImagePage imagePage)
                {
                    Pages.Add(page, new MultiFileProjectSnapshotPage(imagePage.SourceFile, imagePage.TargetFile, imagePage.TargetFolder!,
                        imagePage.FileNameInfo!.DesiredName, imagePage.Filter, imagePage.Brightness, imagePage.Contrast));
                }
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<Dictionary<IProjectPage, TargetFile?>> TrySaveAsync(DispatcherQueue uiDispatcherQueue)
        {
            Dictionary<IProjectPage, TargetFile?> result = new();

            // save images to target folders
            try
            {
                foreach (KeyValuePair<IProjectPage, MultiFileProjectSnapshotPage> page in Pages)
                {
                    TargetFile generatedTargetFile;
                    if (Format == TargetFormat.SinglePagePDF)
                    {
                        // need to generate PDF file
                        Dictionary<IProjectPage, IProjectSnapshotPage> pdfPages = new()
                        {
                            { page.Key, page.Value }
                        };
                        Dictionary<IProjectPage, TargetFile?> pdfResult = await PdfProject.CreatePdfFromPagesAsync(pdfPages, page.Value.TargetFile, page.Value.DesiredFileName, page.Value.TargetFolder, SettingsService.SettingOcrPdfs, uiDispatcherQueue);
                        generatedTargetFile = pdfResult[page.Key];
                    }
                    else if (page.Value.Filter != ImageFilter.None || FileExtensionToTargetFormat(page.Value.SourceFile.FileType) != Format)
                    {
                        // encoding necessary ~> prepare file
                        if (page.Value.TargetFile == null)
                        {
                            StorageFile file = await page.Value.TargetFolder.CreateFileAsync(page.Value.DesiredFileName, CreationCollisionOption.GenerateUniqueName);
                            generatedTargetFile = new(file, await file.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowOnlyReaders));
                        }
                        else
                        {
                            generatedTargetFile = page.Value.TargetFile;
                        }

                        // perform decoding and encoding
                        await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, async () =>
                        {
                            using (IRandomAccessStream sourceStream = await page.Value.SourceFile.OpenAsync(FileAccessMode.Read))
                            {
                                BitmapPropertySet propertySet = new BitmapPropertySet();
                                if (Format == TargetFormat.JPG)
                                {
                                    // prevent large JPEG files by setting quality
                                    propertySet.Add("ImageQuality", new BitmapTypedValue(jpegQuality, Windows.Foundation.PropertyType.Single));
                                }

                                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(ProjectBase.GetBitmapEncoderIdForFile(generatedTargetFile.File), generatedTargetFile.FileStream, propertySet);

                                if (page.Value.Filter != ImageFilter.None)
                                {
                                    // use Win2D effects pipeline
                                    await ProjectBase.ApplyEffectsAsync(sourceStream, encoder, page.Value.Filter, page.Value.Brightness, page.Value.Contrast);
                                }
                                else
                                {
                                    // just decode and encode
                                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream);
                                    using SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                                    encoder.SetSoftwareBitmap(softwareBitmap);
                                    await encoder.FlushAsync();
                                }
                            }                                
                        });
                    }
                    else
                    {
                        // correct format, just copy
                        if (page.Value.TargetFile != null)
                        {
                            StorageFile file = await page.Value.SourceFile.CopyAsync(page.Value.TargetFolder, page.Value.TargetFile.File.Name, NameCollisionOption.ReplaceExisting);

                            if (file.Name != page.Value.DesiredFileName)
                                await file.RenameAsync(page.Value.DesiredFileName, NameCollisionOption.GenerateUniqueName);

                            generatedTargetFile = new(file, await file.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowOnlyReaders));
                        }
                        else
                        {
                            StorageFile file = await page.Value.SourceFile.CopyAsync(page.Value.TargetFolder, page.Value.DesiredFileName, NameCollisionOption.GenerateUniqueName);
                            generatedTargetFile = new(file, await file.OpenAsync(FileAccessMode.ReadWrite, StorageOpenOptions.AllowOnlyReaders));
                        }
                    }
                    result.Add(page.Key, generatedTargetFile);
                }
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "ImageProjectSnapshot - Failed to save images to target folder");
            }

            return result;
        }
    }
}
