using Microsoft.UI.Dispatching;
using Scanner.Models;
using System;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Scanner.Extensions;

public static class ScanOptionsExtensions
{
    public static ImageFilter GetFilter(this ScanOptions scanOptions)
    {
        switch (scanOptions.ColorMode)
        {
            case ScannerColorMode.None:
            case ScannerColorMode.Color:
            case ScannerColorMode.Automatic:
                return ImageFilter.None;
            case ScannerColorMode.Grayscale:
                return ImageFilter.Grayscale;
            case ScannerColorMode.Monochrome:
                return ImageFilter.Monochrome;
            default:
                throw new ArgumentException("Failed to determine page's Filter for given configuration");
        }
    }

    public static ImageFilter GetBaseFilter(this ScanOptions scanOptions)
    {
        switch (scanOptions.SourceMode)
        {
            case ScannerSource.Auto:
                if (scanOptions.Scanner.IsColorAllowedInAnyMode)
                    return ImageFilter.None;
                else if (scanOptions.Scanner.IsGrayscaleAllowedInAnyMode)
                    return ImageFilter.Grayscale;
                else
                    return ImageFilter.Monochrome;

            case ScannerSource.Flatbed:
                switch (scanOptions.ColorMode)
                {
                    case ScannerColorMode.None:
                    case ScannerColorMode.Color:
                    case ScannerColorMode.Automatic:
                    default:
                        return ImageFilter.None;
                    case ScannerColorMode.Grayscale:
                        if (scanOptions.Scanner.IsFlatbedGrayscaleAllowed)
                            return ImageFilter.Grayscale;
                        else
                            return ImageFilter.None;
                    case ScannerColorMode.Monochrome:
                        if (scanOptions.Scanner.IsFlatbedMonochromeAllowed)
                            return ImageFilter.Monochrome;
                        else if (scanOptions.Scanner.IsFlatbedColorAllowed)
                            return ImageFilter.None;
                        else
                            return ImageFilter.Grayscale;
                }

            case ScannerSource.Feeder:
                switch (scanOptions.ColorMode)
                {
                    case ScannerColorMode.None:
                    case ScannerColorMode.Color:
                    case ScannerColorMode.Automatic:
                    default:
                        return ImageFilter.None;
                    case ScannerColorMode.Grayscale:
                        if (scanOptions.Scanner.IsFeederGrayscaleAllowed)
                            return ImageFilter.Grayscale;
                        else
                            return ImageFilter.None;
                    case ScannerColorMode.Monochrome:
                        if (scanOptions.Scanner.IsFeederMonochromeAllowed)
                            return ImageFilter.Monochrome;
                        else if (scanOptions.Scanner.IsFeederColorAllowed)
                            return ImageFilter.None;
                        else
                            return ImageFilter.Grayscale;
                }

            case ScannerSource.None:
            default:
                return ImageFilter.None;
        }
    }
}
