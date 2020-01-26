using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.IO;
using Windows.Storage;

namespace ImageToPDF
{
    class Program
    {
        //[STAThread]
        static void Main(string[] args)
        {
            Conversion();
        }


        static async void Conversion()
        {
            try
            {
                string srcFileName = (string)ApplicationData.Current.LocalSettings.Values["sourceFileName"];
                FileStream sourceFile = File.OpenRead(ApplicationData.Current.TemporaryFolder.Path + Path.DirectorySeparatorChar + srcFileName);

                // create PDF with a single page
                PdfDocument document = new PdfDocument();
                PdfPage page = document.AddPage();
                page.Width = (uint) ApplicationData.Current.LocalSettings.Values["sourceFileWidth"];
                page.Height = (uint) ApplicationData.Current.LocalSettings.Values["sourceFileHeight"];

                // draw image
                XGraphics gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(XImage.FromStream(sourceFile), 0, 0, page.Width, page.Height);
                sourceFile.Dispose();

                // save to target file
                string trgFileName = (string) ApplicationData.Current.LocalSettings.Values["targetFileName"];
                document.Save(ApplicationData.Current.TemporaryFolder.Path + Path.DirectorySeparatorChar + trgFileName);
                document.Close();
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
