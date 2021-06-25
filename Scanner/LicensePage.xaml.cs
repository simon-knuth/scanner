using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

using static Enums;


namespace Scanner
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LicensePage : Page
    {
        public LicensePage()
        {
            this.InitializeComponent();
        }

        private void HyperlinkLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            ThirdPartyLicense selected = 0;

            if (sender == HyperlinkLicenseMicrosoftAppCenterAnalytics)
            {
                selected = ThirdPartyLicense.MicrosoftAppCenterAnalytics;
            }
            else if (sender == HyperlinkLicenseMicrosoftAppCenterCrashes)
            {
                selected = ThirdPartyLicense.MicrosoftAppCenterCrashes;
            }
            else if (sender == HyperlinkLicenseMicrosoftNETCoreUniversalWindowsPlatform)
            {
                selected = ThirdPartyLicense.MicrosoftNETCoreUniversalWindowsPlatform;
            }
            else if (sender == HyperlinkLicenseMicrosoftServicesStoreEngagement)
            {
                selected = ThirdPartyLicense.MicrosoftServicesStoreEngagement;
            }
            else if (sender == HyperlinkLicenseMicrosoftAppCenterCrashes)
            {
                selected = ThirdPartyLicense.MicrosoftAppCenterCrashes;
            }
            else if (sender == HyperlinkLicenseMicrosoftToolkitUwpNotifications)
            {
                selected = ThirdPartyLicense.MicrosoftToolkitUwpNotifications;
            }
            else if (sender == HyperlinkLicenseMicrosoftToolkitUwpUIAnimations)
            {
                selected = ThirdPartyLicense.MicrosoftToolkitUwpUIAnimations;
            }
            else if (sender == HyperlinkLicenseMicrosoftToolkitUwpUIControls)
            {
                selected = ThirdPartyLicense.MicrosoftToolkitUwpUIControls;
            }
            else if (sender == HyperlinkLicenseMicrosoftToolkitUwpUILottie)
            {
                selected = ThirdPartyLicense.MicrosoftToolkitUwpUILottie;
            }
            else if (sender == HyperlinkLicenseMicrosoftUIXAML)
            {
                selected = ThirdPartyLicense.MicrosoftUIXAML;
            }
            else if (sender == HyperlinkLicensePDFsharp)
            {
                selected = ThirdPartyLicense.PDFsharp;
            }
            else if (sender == HyperlinkLicenseQueryStringNET)
            {
                selected = ThirdPartyLicense.QueryStringNET;
            }
            else if (sender == HyperlinkLicenseWin2Duwp)
            {
                selected = ThirdPartyLicense.Win2Duwp;
            }
            else if (sender == HyperlinkLicenseSerilog)
            {
                selected = ThirdPartyLicense.Serilog;
            }
            else if (sender == HyperlinkLicenseSerilogExceptions)
            {
                selected = ThirdPartyLicense.SerilogExceptions;
            }
            else if (sender == HyperlinkLicenseSerilogSinksAsync)
            {
                selected = ThirdPartyLicense.SerilogSinksAsync;
            }
            else if (sender == HyperlinkLicenseSerilogSinksFile)
            {
                selected = ThirdPartyLicense.SerilogSinksFile;
            }

            Frame.Navigate(typeof(LicenseDetailPage), selected, new SlideNavigationTransitionInfo()
            { Effect = SlideNavigationTransitionEffect.FromRight });
        }
    }
}
