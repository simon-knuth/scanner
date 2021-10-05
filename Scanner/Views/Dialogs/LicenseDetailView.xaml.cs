using System;
using System.IO;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Navigation;

using static Enums_old;


namespace Scanner
{
    public sealed partial class LicenseDetailView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ThirdPartyLicense License;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public LicenseDetailView()
        {
            this.InitializeComponent();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            License = (ThirdPartyLicense)e.Parameter;
        }


        private string GetPathForLicense()
        {
            switch (License)
            {
                case ThirdPartyLicense.MicrosoftAppCenterAnalytics:
                    return "ms-appx:///License Texts/LicenseMicrosoftAppCenterAnalytics.txt";
                case ThirdPartyLicense.MicrosoftAppCenterCrashes:
                    return "ms-appx:///License Texts/LicenseMicrosoftAppCenterCrashes.txt";
                case ThirdPartyLicense.MicrosoftNETCoreUniversalWindowsPlatform:
                    return "ms-appx:///License Texts/LicenseMicrosoftNETCoreUniversalWindowsPlatform.txt";
                case ThirdPartyLicense.MicrosoftServicesStoreEngagement:
                    return "ms-appx:///License Texts/LicenseMicrosoftServicesStoreEngagement.txt";
                case ThirdPartyLicense.MicrosoftToolkitUwpNotifications:
                    return "ms-appx:///License Texts/LicenseMicrosoftToolkitUwpNotifications.txt";
                case ThirdPartyLicense.MicrosoftToolkitUwpUIAnimations:
                    return "ms-appx:///License Texts/LicenseMicrosoftToolkitUwpUIAnimations.txt";
                case ThirdPartyLicense.MicrosoftToolkitUwpUIControls:
                    return "ms-appx:///License Texts/LicenseMicrosoftToolkitUwpUIControls.txt";
                case ThirdPartyLicense.MicrosoftToolkitUwpUILottie:
                    return "ms-appx:///License Texts/LicenseMicrosoftToolkitUwpUILottie.txt";
                case ThirdPartyLicense.MicrosoftUIXAML:
                    return "ms-appx:///License Texts/LicenseMicrosoftUIXAML.txt";
                case ThirdPartyLicense.PDFsharp:
                    return "ms-appx:///License Texts/LicensePDFsharp.txt";
                case ThirdPartyLicense.QueryStringNET:
                    return "ms-appx:///License Texts/LicenseQueryStringNET.txt";
                case ThirdPartyLicense.Win2Duwp:
                    return "ms-appx:///License Texts/LicenseWin2Duwp.txt";
                case ThirdPartyLicense.Serilog:
                    return "ms-appx:///License Texts/LicenseSerilog.txt";
                case ThirdPartyLicense.SerilogExceptions:
                    return "ms-appx:///License Texts/LicenseSerilogExceptions.txt";
                case ThirdPartyLicense.SerilogSinksAsync:
                    return "ms-appx:///License Texts/LicenseSerilogSinksAsync.txt";
                case ThirdPartyLicense.SerilogSinksFile:
                    return "ms-appx:///License Texts/LicenseSerilogSinksFile.txt";
                default:
                    throw new ArgumentException("No file path for license.");
            }
        }


        private async void Page_Loading(FrameworkElement sender, object args)
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(GetPathForLicense()));
                using (var inputStream = await file.OpenReadAsync())
                using (var classicStream = inputStream.AsStreamForRead())
                using (var streamReader = new StreamReader(classicStream))
                {
                    while (streamReader.Peek() >= 0)
                    {
                        Paragraph paragraph = new Paragraph();
                        Run run = new Run();
                        run.Text = streamReader.ReadLine();
                        paragraph.Inlines.Add(run);

                        RichTextBlockLicense.Blocks.Add(paragraph);
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
