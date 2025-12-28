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
using Scanner.Helpers;
using Windows.Foundation;
using Scanner.Extensions;

namespace Scanner.Models
{
    public abstract class ScanArea
    {

    }

    public abstract class RectScanArea : ScanArea
    {
        public Rect GetRect(IImageScannerSourceConfiguration sourceConfig)
        {
            if (sourceConfig is ImageScannerFlatbedConfiguration flatbedConfig)
            {
                Rect result = GetRect(
                    0,
                    flatbedConfig.MaxScanArea.Width - 1,
                    0,
                    flatbedConfig.MaxScanArea.Height - 1,
                    flatbedConfig.MinScanArea.Width,
                    flatbedConfig.MaxScanArea.Width,
                    flatbedConfig.MinScanArea.Height,
                    flatbedConfig.MaxScanArea.Height);

                // ensure limits
                result.Width = Math.Min(Math.Max(result.Width, flatbedConfig.MinScanArea.Width), flatbedConfig.MaxScanArea.Width - result.X);
                result.Height = Math.Min(Math.Max(result.Height, flatbedConfig.MinScanArea.Height), flatbedConfig.MaxScanArea.Height - result.Y);

                return result;
            }
            else if (sourceConfig is ImageScannerFeederConfiguration feederConfig)
            {
                return GetRect(
                        0,
                        feederConfig.PageSizeDimensions.Width,
                        0,
                        feederConfig.PageSizeDimensions.Height,
                        feederConfig.MinScanArea.Width,
                        feederConfig.PageSizeDimensions.Width,
                        feederConfig.MinScanArea.Height,
                        feederConfig.PageSizeDimensions.Height);
            }
            else
            {
                throw new ArgumentException("Failed to get ScanArea rectangle for source config");
            }
        }

        internal abstract Rect GetRect(double minX, double maxX, double minY, double MaxY, double minWidth, double maxWidth, double minHeight, double maxHeight);
    }

    public class AutoCropArea : ScanArea
    {
        public ScannerAutoCropMode AutoCropMode { get; set; }
    }

    [ObservableObject]
    public partial class PaperSizeArea : RectScanArea
    {
        [ObservableProperty]
        private PaperSize paperSize;

        [ObservableProperty]
        private ScanCorner corner;

        [ObservableProperty]
        private ScanOrientation orientation;

        internal override Rect GetRect(double minX, double maxX, double minY, double MaxY, double minWidth, double maxWidth, double minHeight, double maxHeight)
        {
            Rect rect = PaperSize.ToRect();

            double width = Measurement.FromCentimeters(rect.Width / 10).GetInches();
            double height = Measurement.FromCentimeters(rect.Height / 10).GetInches();

            // get base values
            double x, y;
            switch (Corner)
            {
                default:
                case ScanCorner.TopLeft:
                    x = minX;
                    y = minY;
                    break;
                case ScanCorner.TopRight:
                    x = maxX - width;
                    y = minY;
                    break;
                case ScanCorner.BottomRight:
                    x = maxX - width;
                    y = MaxY - height;
                    break;
                case ScanCorner.BottomLeft:
                    x = minX;
                    y = MaxY - height;
                    break;
            }

            // ensure limits
            x = Math.Min(Math.Max(x, minX), maxX);
            y = Math.Min(Math.Max(y, minY), MaxY);
            width = Math.Min(Math.Max(width, minWidth), maxWidth);
            height = Math.Min(Math.Max(height, minHeight), maxHeight);

            return new Rect(x, y, width, height);
        }
    }

    public class PreviewSelectionArea : RectScanArea
    {
        public Rect SelectedRegion { get; set; }

        internal override Rect GetRect(double minX, double maxX, double minY, double MaxY, double minWidth, double maxWidth, double minHeight, double maxHeight)
        {
            double width = SelectedRegion.Width;
            double height = SelectedRegion.Height;

            // ensure limits
            double x = Math.Min(Math.Max(SelectedRegion.X, minX), maxX);
            double y = Math.Min(Math.Max(SelectedRegion.Y, minY), MaxY);
            width = Math.Min(Math.Max(width, minWidth), maxWidth);
            height = Math.Min(Math.Max(height, minHeight), maxHeight);

            return new Rect(x, y, width, height);
        }
    }

    public enum ScanCorner
    {
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }

    public enum ScanOrientation
    {
        Portrait,
        Landscape
    }
}