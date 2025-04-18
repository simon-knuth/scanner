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
using Scanner.Services.Interfaces;
using Scanner.Models.Interfaces;
using System.Threading;
using Windows.Devices.Enumeration;
using Serilog.Sinks.File;
using Serilog;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Serilog.Formatting.Compact;
using Serilog.Exceptions;
using CommunityToolkit.Mvvm.DependencyInjection;
using Windows.Graphics.Imaging;
using Tesseract;
using System.Diagnostics;

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

        public void GeneratePdf(List<StorageFile> Files, string targetFilePath)
        {
            using (TesseractEngine engine = new TesseractEngine(trainingDataFolderPath, "eng"))
            {
                using (IResultRenderer renderer = PdfResultRenderer.CreatePdfRenderer(targetFilePath, trainingDataFolderPath, false))
                {
                    renderer.BeginDocument("Scan");
                    foreach (StorageFile file in Files)
                    {
                        using (Pix image = Pix.LoadFromFile(file.Path))
                        {
                            using (Page page = engine.Process(image))
                            {
                                renderer.AddPage(page);
                            }
                        }
                    }
                }
            }
        }
    }
}
