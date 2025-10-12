using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;

namespace Scanner
{
    internal static partial class AppConfig
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Analytics & Diagnostics
        public static int MaxDiagnosticEventsPerSession = 3;
        public static double DefaultRate = 0.1;
        public static double CrashRate = 1.0;
        public static double ErrorRate = 0.3;
        public static double WarningRate = 0.1;
        public static double CrashAttachmentRate = 0.25;
        public static double ErrorAttachmentRate = 0.05;
        public static double WarningAttachmentRate = 0.01;
        #endregion

        public static TimeSpan ConsecutiveAtomicActionMergeTime = TimeSpan.FromSeconds(1);

        public const int DefaultBrightness = 0;
        public const int DefaultContrast = 0;

        public const float DocumentsResolution = 300;      // the recommended resolution for documents
        public const float PhotosResolution = 500;         // the recommended resolution for photos

        public static Uri PrivacyPolicyUri = new Uri("https://simon-knuth.github.io/scanner/privacy-policy");
        public static Uri SimonUri = new Uri("https://simon-knuth.github.io/");
        public static Uri DonationUri = new Uri("https://www.paypal.com/donate?hosted_button_id=TLR5GM8NKE3L2&amp;source=url");


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        static AppConfig()
        {
            
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    }
}
