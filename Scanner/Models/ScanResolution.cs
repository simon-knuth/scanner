using System;
using System.ComponentModel.DataAnnotations;
using Windows.Devices.Scanners;
using static Utilities;

namespace Scanner.Models
{
    public class ScanResolution
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        [Required(ErrorMessage = "ImageScannerResolution is required")]
        public ImageScannerResolution Resolution;

        [Required(ErrorMessage = "Annotation is required")]
        public ResolutionAnnotation Annotation;

        [Required(ErrorMessage = "FriendlyText is required")]
        public string FriendlyText;

        public const float DocumentsResolution = 300;      // the recommended resolution for documents
        public const float PhotosResolution = 500;         // the recommended resolution for photos


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanResolution(ImageScannerResolution resolution, ResolutionAnnotation annotation)
        {
            Resolution = resolution;
            Annotation = annotation;
            FriendlyText = GenerateFriendlyText();
        }

        public ScanResolution(float resolution, ResolutionAnnotation annotation)
        {
            Resolution = new ImageScannerResolution { DpiX = resolution, DpiY = resolution };
            Annotation = annotation;
            FriendlyText = GenerateFriendlyText();
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private string GenerateFriendlyText()
        {
            switch (Annotation)
            {
                case ResolutionAnnotation.Default:
                    return String.Format(LocalizedString("OptionScanOptionsResolutionDefault"), Resolution.DpiX);
                case ResolutionAnnotation.Documents:
                    return String.Format(LocalizedString("OptionScanOptionsResolutionDocuments"), Resolution.DpiX);
                case ResolutionAnnotation.Photos:
                    return String.Format(LocalizedString("OptionScanOptionsResolutionPhotos"), Resolution.DpiX);
                case ResolutionAnnotation.None:
                default:
                    return String.Format(LocalizedString("OptionScanOptionsResolution"), Resolution.DpiX);
            }
        }

        public override string ToString()
        {
            return FriendlyText;
        }
    }

    /// <summary>
    ///     The possible properties a resolution value can have.
    /// </summary>
    public enum ResolutionAnnotation
    {
        None = 0,
        Default = 1,
        Documents = 2,
        Photos = 3,
    }
}
