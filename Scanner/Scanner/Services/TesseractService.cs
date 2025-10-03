using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using Serilog;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;
using Serilog.Sinks.File;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;
using static Scanner.Models.PdfProjectSnapshot;

namespace Scanner.Services
{
    internal class TesseractService : ITesseractService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private const float minOrientationConfidence = 3;

        private TesseractEngine osdEngine = new TesseractEngine(trainingDataFolderPath, "osd");

        private static string trainingDataFolderPath = Path.GetDirectoryName(Environment.ProcessPath)
                + Path.DirectorySeparatorChar
                + "Resources"
                + Path.DirectorySeparatorChar
                + "Tesseract Training Data"
                + Path.DirectorySeparatorChar
                + "tessdata";


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public TesseractService()
        {

        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public BitmapRotation? GetRecommendedRotation(StorageFile file)
        {
            // load file
            using (Pix image = Pix.LoadFromFile(file.Path))
            {
                // limit area for processing
                Rect region = new Rect(0, 0, image.Width / 2, image.Height / 2);

                // analyze orientation
                using (Page page = osdEngine.Process(image, region, PageSegMode.AutoOsd))
                {
                    int orientation = 0;
                    float confidence = 0;
                    try
                    {
                        page.DetectBestOrientation(out orientation, out confidence);
                    }
                    catch (Exception) { }

                    if (confidence < minOrientationConfidence) return null;         // confidence too low

                    switch (orientation)
                    {
                        case 0:
                            return BitmapRotation.None;
                        case 90:
                            return BitmapRotation.Clockwise270Degrees;
                        case 180:
                            return BitmapRotation.Clockwise180Degrees;
                        case 270:
                            return BitmapRotation.Clockwise90Degrees;
                        default:
                            return null;
                    }
                }
            }
        }

        public async Task GeneratePdfAsync(List<PdfProjectSnapshotPage> pages, string targetFilePath, DispatcherQueue uiDispatcherQueue)
        {
            using (TesseractEngine engine = new TesseractEngine(trainingDataFolderPath, "eng"))
            {
                using (IResultRenderer renderer = PdfResultRenderer.CreatePdfRenderer(targetFilePath, trainingDataFolderPath, false))
                {
                    renderer.BeginDocument("Scan");
                    foreach (PdfProjectSnapshotPage snapshotPage in pages)
                    {
                        if (snapshotPage.Filter == ImageFilter.None && snapshotPage.Brightness == 0 && snapshotPage.Contrast == 0)
                        {
                            // source file can be used directly
                            using (Pix image = Pix.LoadFromFile(snapshotPage.SourceFile.Path))
                            {
                                using (Page pdfPage = engine.Process(image))
                                {
                                    renderer.AddPage(pdfPage);
                                }
                            }
                        }
                        else
                        {
                            // source file needs to be adjusted first
                            using (IRandomAccessStream sourceStream = await snapshotPage.SourceFile.OpenAsync(FileAccessMode.Read))
                            using (IRandomAccessStream targetStream = new InMemoryRandomAccessStream())
                            {
                                await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.Low, async () =>
                                {
                                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(ProjectBase.GetBitmapEncoderIdForFile(snapshotPage.SourceFile), targetStream);
                                    await ProjectBase.ApplyEffectsAsync(sourceStream, encoder, snapshotPage.Filter, snapshotPage.Brightness, snapshotPage.Contrast);
                                });

                                // reset stream position and load into a byte array
                                targetStream.Seek(0);
                                using (DataReader reader = new DataReader(targetStream.GetInputStreamAt(0)))
                                {
                                    uint size = (uint)targetStream.Size;
                                    await reader.LoadAsync(size);
                                    byte[] imageBytes = new byte[size];
                                    reader.ReadBytes(imageBytes);

                                    // load the processed image into Tesseract
                                    using (Pix image = Pix.LoadFromMemory(imageBytes))
                                    {
                                        using (Page pdfPage = engine.Process(image))
                                        {
                                            renderer.AddPage(pdfPage);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
