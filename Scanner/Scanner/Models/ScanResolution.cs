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
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;

using static Scanner.Helpers.Helpers;

namespace Scanner.Models
{
    public partial class ScanResolution
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ImageScannerResolution Resolution;

        public ResolutionAnnotation Annotation;

        public string FriendlyText;
        public string FriendlyShortText;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanResolution(ImageScannerResolution resolution, ResolutionAnnotation annotation)
        {
            Resolution = resolution;
            Annotation = annotation;
            FriendlyText = GenerateFriendlyText();
            FriendlyShortText = String.Format(GetLocalized("OptionScanOptionsResolution"), Resolution.DpiX);
        }

        public ScanResolution(float resolution, ResolutionAnnotation annotation)
        {
            Resolution = new ImageScannerResolution { DpiX = resolution, DpiY = resolution };
            Annotation = annotation;
            FriendlyText = GenerateFriendlyText();
            FriendlyShortText = String.Format(GetLocalized("OptionScanOptionsResolution"), Resolution.DpiX);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private string GenerateFriendlyText()
        {
            switch (Annotation)
            {
                case ResolutionAnnotation.Default:
                    return String.Format(GetLocalized("OptionScanOptionsResolutionDefault"), Resolution.DpiX);
                case ResolutionAnnotation.Documents:
                    return String.Format(GetLocalized("OptionScanOptionsResolutionDocuments"), Resolution.DpiX);
                case ResolutionAnnotation.Photos:
                    return String.Format(GetLocalized("OptionScanOptionsResolutionPhotos"), Resolution.DpiX);
                case ResolutionAnnotation.None:
                default:
                    return String.Format(GetLocalized("OptionScanOptionsResolution"), Resolution.DpiX);
            }
        }

        public override string ToString()
        {
            return FriendlyText;
        }
    }

    /// <summary>
    ///     The possible annotations a resolution value can have.
    /// </summary>
    public enum ResolutionAnnotation
    {
        None = 0,
        Default = 1,
        Documents = 2,
        Photos = 3,
    }
}