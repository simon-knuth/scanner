using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
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
using static Scanner.Services.Interfaces.IPdfService;

namespace Scanner.Services
{
    internal class PdfService : IPdfService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PdfService()
        {

        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void ScalePdf(string path, PdfPageScalingInstruction[] instructions)
        {
            using PdfDocument outputDoc = new();
            using (XPdfForm form = XPdfForm.FromFile(path))
            {
                int i = 0;
                foreach (PdfPageScalingInstruction instruction in instructions)
                {
                    form.PageIndex = i;

                    // create new page with target size
                    PdfPage newPage = outputDoc.AddPage();
                    newPage.Width = XUnit.FromPoint(form.PointWidth * instruction.TargetScalingFactor);
                    newPage.Height = XUnit.FromPoint(form.PointHeight * instruction.TargetScalingFactor);

                    // draw original page content onto new page with scaling
                    using (XGraphics gfx = XGraphics.FromPdfPage(newPage))
                    {
                        gfx.ScaleTransform(instruction.TargetScalingFactor, instruction.TargetScalingFactor);
                        gfx.DrawImage(form, 0, 0);
                    }

                    i++;
                }
            }

            outputDoc.Save(path);
        }
    }
}
