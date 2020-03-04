using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Storage;


namespace ImageToPDF
{
    class Program
    {
        public const string appServiceName = "pdfConversionService";
        public static AppServiceConnection appServiceConnection;
        public static ManualResetEvent serviceCallEvent = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            ConnectToService();

            Conversion();

            while (true)
            {
                serviceCallEvent.WaitOne();

                serviceCallEvent = new ManualResetEvent(false);

                ConnectToService();

                Conversion();
            }
        }



        /// <summary>
        ///     Connects to the app service, which links the app to its UWP container.
        /// </summary>
        private static async void ConnectToService()
        {
            appServiceConnection = new AppServiceConnection();
            appServiceConnection.AppServiceName = appServiceName;
            appServiceConnection.PackageFamilyName = Package.Current.Id.FamilyName;
            appServiceConnection.RequestReceived += (x, y) =>
            {
                switch (y.Request.Message["REQUEST"])
                {
                    case "CONVERT":
                        serviceCallEvent.Set();
                        break;
                }
            };
            appServiceConnection.ServiceClosed += (x, y) => Environment.Exit(1);

            AppServiceConnectionStatus status = await appServiceConnection.OpenAsync();
        }



        /// <summary>
        ///     Converts the source file to a PDF file format.
        /// </summary>
        private static async void Conversion()
        {
            try
            {
                // get source image file and open in reading mode (!!!)
                string srcFileName = (string)ApplicationData.Current.LocalSettings.Values["pdfSourceFileName"];
                FileStream srcFile = File.OpenRead(ApplicationData.Current.TemporaryFolder.Path + Path.DirectorySeparatorChar + srcFileName);

                // create single-page PDF
                PdfDocument document = new PdfDocument();
                PdfPage page = document.AddPage();
                page.Width = (uint)ApplicationData.Current.LocalSettings.Values["sourceFileWidth"];
                page.Height = (uint)ApplicationData.Current.LocalSettings.Values["sourceFileHeight"];

                // draw image
                XGraphics gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(XImage.FromStream(srcFile), 0, 0, page.Width, page.Height);
                srcFile.Dispose();

                // save to target file
                string trgFileName = (string)ApplicationData.Current.LocalSettings.Values["targetFileName"];
                FileStream resultStream = new FileStream(ApplicationData.Current.TemporaryFolder.Path + Path.DirectorySeparatorChar + trgFileName,
                                                                FileMode.CreateNew, FileAccess.Write);
                document.Save(resultStream, true);
            }
            catch (Exception)
            {
                // notify UWP container that conversion failed
                await SendMessageAsync("RESULT", "FAILURE");
            }

            // notify UWP container that conversion succeeded
            await SendMessageAsync("RESULT", "SUCCESS");
        }



        /// <summary>
        ///     Sends a message using <see cref="appServiceConnection"/>.
        /// </summary>
        /// <param name="key">The message key.</param>
        /// <param name="value">The corresponding value.</param>
        /// <returns>True, if message was sent successfully, false if not.</returns>
        private static async Task<bool> SendMessageAsync(string key, string value)
        {
            try
            {
                ValueSet message = new ValueSet();
                message.Add(key, value);
                await appServiceConnection.SendMessageAsync(message);
            }
            catch (Exception)
            {
                // message could not be sent
                return false;
            }

            // message sent successfully
            return true;
        }
    }
}
