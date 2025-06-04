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
    public partial class ImageProjectSnapshot : IProjectSnapshot
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
        private const double jpegQuality = 0.85;
        #endregion

        public TargetFormat Format { get; private set; }

        /// <remarks>
        /// Editing the <see cref="IProjectPage"/> references from the snapshot is not allowed.
        /// </remarks>
        public Dictionary<IProjectPage, ImageProjectSnapshotPage> Pages { get; private set; } = new();


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ImageProjectSnapshot(ImageProject project)
        {
            Format = project.Format;

            foreach (IProjectPage page in project.Pages)
            {
                if (page is ImagePage imagePage)
                {
                    Pages.Add(page, new ImageProjectSnapshotPage(imagePage.SourceFile, imagePage.TargetFile, imagePage.TargetFolder!,
                        imagePage.FileNameInfo!.DesiredName, imagePage.Filter));
                }
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<Dictionary<IProjectPage, StorageFile?>> TrySaveAsync(DispatcherQueue uiDispatcherQueue)
        {
            Dictionary<IProjectPage, StorageFile?> result = new();

            // save images to target folders
            try
            {
                foreach (KeyValuePair<IProjectPage, ImageProjectSnapshotPage> page in Pages)
                {
                    StorageFile generatedFile;
                    if (page.Value.Filter != ImageFilter.None || FileExtensionToTargetFormat(page.Value.SourceFile.FileType) != Format)
                    {
                        // encoding necessary ~> prepare file
                        if (page.Value.TargetFile == null)
                        {
                            generatedFile = await page.Value.TargetFolder.CreateFileAsync(page.Value.DesiredFileName, CreationCollisionOption.GenerateUniqueName);
                        }
                        else
                        {
                            generatedFile = page.Value.TargetFile;
                        }

                        // perform decoding and encoding
                        await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, async () =>
                        {
                            using (IRandomAccessStream sourceStream = await page.Value.SourceFile.OpenAsync(FileAccessMode.Read))
                            using (IRandomAccessStream targetStream = await generatedFile.OpenAsync(FileAccessMode.ReadWrite))
                            {
                                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(ProjectBase.GetBitmapEncoderIdForFile(generatedFile), targetStream);

                                if (page.Value.Filter != ImageFilter.None)
                                {
                                    // use Win2D effects pipeline
                                    await ProjectBase.ApplyFilterAsync(sourceStream, encoder, page.Value.Filter);
                                }
                                else
                                {
                                    // just decode and encode
                                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream);
                                    using (SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync())
                                    {
                                        encoder.SetSoftwareBitmap(softwareBitmap);
                                        await encoder.FlushAsync();
                                    }
                                }
                                
                            }                                
                        });
                    }
                    else
                    {
                        // correct format, just copy
                        if (page.Value.TargetFile != null)
                        {
                            generatedFile = await page.Value.SourceFile.CopyAsync(page.Value.TargetFolder, page.Value.TargetFile.Name, NameCollisionOption.ReplaceExisting);

                            if (generatedFile.Name != page.Value.DesiredFileName)
                            {
                                await generatedFile.RenameAsync(page.Value.DesiredFileName, NameCollisionOption.GenerateUniqueName);
                            }
                        }
                        else
                        {
                            generatedFile = await page.Value.SourceFile.CopyAsync(page.Value.TargetFolder, page.Value.DesiredFileName, NameCollisionOption.GenerateUniqueName);
                        }
                    }
                    result.Add(page.Key, generatedFile);
                }
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "ImageProjectSnapshot - Failed to save images to target folder");
            }

            return result;
        }

        private async Task<BitmapEncoder> CreateBitmapEncoderAsync(IRandomAccessStream stream)
        {
            // get encoder ID
            Guid encoderId;
            switch (Format)
            {
                case TargetFormat.JPG:
                    encoderId = BitmapEncoder.JpegEncoderId;
                    break;
                case TargetFormat.PNG:
                    encoderId = BitmapEncoder.PngEncoderId;
                    break;
                case TargetFormat.TIFF:
                    encoderId = BitmapEncoder.TiffEncoderId;
                    break;
                case TargetFormat.BMP:
                    encoderId = BitmapEncoder.BmpEncoderId;
                    break;
                default:
                    throw new ArgumentException($"CreateBitmapEncoderAsync received invalid format {Format}");
            }

            // create encoder
            if (Format == TargetFormat.JPG)
            {
                // prevent large JPG size
                var propertySet = new BitmapPropertySet();
                var qualityValue = new BitmapTypedValue(jpegQuality, Windows.Foundation.PropertyType.Single);
                propertySet.Add("ImageQuality", qualityValue);

                stream.Size = 0;
                return await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, propertySet);
            }
            else
            {
                return await BitmapEncoder.CreateAsync(encoderId, stream);
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public record ImageProjectSnapshotPage(StorageFile SourceFile, StorageFile? TargetFile, StorageFolder TargetFolder,
            string? DesiredFileName, ImageFilter Filter);
    }
}
