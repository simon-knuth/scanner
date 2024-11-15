using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
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

        [STAThread]
        static async Task Main(string[] args)
        {
            await ConnectToService();

            await Conversion();

            //while (true)
            //{
                //serviceCallEvent.WaitOne();

                //serviceCallEvent = new ManualResetEvent(false);

                //ConnectToService();

                //Conversion();
            //}
        }



        /// <summary>
        ///     Connects to the app service, which links the app to its UWP container.
        /// </summary>
        private static async Task ConnectToService()
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
            //appServiceConnection.ServiceClosed += (x, y) => Environment.Exit(1);

            AppServiceConnectionStatus status = await appServiceConnection.OpenAsync();
        }



        /// <summary>
        ///     Converts the source file to a PDF file format.
        /// </summary>
        private static async Task Conversion()
        {           
            try
            {
                // get files to construct PDF from
                StorageFolder conversionFolder;
                //var folderPicker = new FolderBrowserDialog();
                //folderPicker.ShowDialog();
                //conversionFolder = await StorageFolder.GetFolderFromPathAsync(folderPicker.SelectedPath);
                conversionFolder = await ApplicationData.Current.TemporaryFolder.GetFolderAsync("conversion");

                IReadOnlyList<StorageFile> conversionFiles = await conversionFolder.GetFilesAsync();

                // sort files
                List<StorageFile> sortedConversionFiles = new List<StorageFile>(conversionFiles);
                sortedConversionFiles.Sort(new ConversionFilesComparer());

                // construct PDF
                using (PdfDocument document = new PdfDocument())
                {
                    foreach (StorageFile file in sortedConversionFiles)
                    {
                        float imageWidth, imageHeight;

                        using (Image image = Image.FromFile(file.Path))
                        {
                            imageWidth = image.Width / image.HorizontalResolution;
                            imageHeight = image.Height / image.VerticalResolution;
                        }

                        using (FileStream srcFile = File.OpenRead(file.Path))
                        {
                            // start new page
                            PdfPage page = document.AddPage();

                            // get measurements
                            page.Width = XUnit.FromInch(imageWidth);
                            page.Height = XUnit.FromInch(imageHeight);

                            // draw image on page
                            using (XGraphics gfx = XGraphics.FromPdfPage(page))
                            {
                                gfx.DrawImage(XImage.FromStream(srcFile), 0, 0, page.Width, page.Height);
                            }
                        }
                    }

                    // save to target file
                    string trgFileName = (string)ApplicationData.Current.LocalSettings.Values["targetFileName"];
                    using (FileStream resultStream = new FileStream(ApplicationData.Current.TemporaryFolder.Path + Path.DirectorySeparatorChar + trgFileName,
                                                                    FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        document.Save(resultStream, true);
                    }
                }
            }
            catch (Exception exc)
            {
                // notify UWP container that conversion failed
                try { ApplicationData.Current.LocalSettings.Values["fullTrustProcessError"] = exc.Message + " | " + exc.StackTrace; }
                catch (Exception) { }
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

    class ConversionFilesComparer : IComparer<StorageFile>
    {
        public int Compare(StorageFile x, StorageFile y)
        {
            return int.Parse(x.DisplayName).CompareTo(int.Parse(y.DisplayName));
        }
    }
}
