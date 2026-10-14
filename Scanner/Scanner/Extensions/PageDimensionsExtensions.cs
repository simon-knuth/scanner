using Microsoft.UI.Dispatching;
using Scanner.Helpers;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Printing;
using Windows.UI.Core;
using static Scanner.Helpers.PageDimensionsHelper;

namespace Scanner.Extensions;

public static class PageDimensionsExtensions
{
    /// <summary>
    /// Maps <see cref="PaperSize"/>s to their corresponding dimensions in mm.
    /// </summary>
    private static readonly Dictionary<PaperSize, Rect> PaperSizes = new()
    {
        { PaperSize.A3, new(0 , 0, 297, 320) },
        { PaperSize.A4, new(0 , 0, 210, 297) },
        { PaperSize.A5, new(0 , 0, 148, 210) },
        { PaperSize.AnsiA, new(0 , 0, 216, 279) },
        { PaperSize.AnsiB, new(0 , 0, 279, 432) },
        { PaperSize.AnsiC, new(0 , 0, 432, 559) },
        //{ PaperSize.Kai16, new(0 , 0, 185, 260) },
        //{ PaperSize.Kai32, new(0 , 0, 130, 185) },
        { PaperSize.Legal, new(0 , 0, 216, 356) },
    };

    /// <summary>
    /// Maps <see cref="PaperSize"/>s to their corresponding <see cref="AspectRatio"/>.
    /// </summary>
    private static readonly Dictionary<PaperSize, AspectRatio> PaperSizeAspectRatios = new()
    {
        { PaperSize.A3, AspectRatio.A },
        { PaperSize.A4, AspectRatio.A },
        { PaperSize.A5, AspectRatio.A },
        { PaperSize.AnsiA, AspectRatio.AnsiA },
        { PaperSize.AnsiB, AspectRatio.AnsiB },
        { PaperSize.AnsiC, AspectRatio.AnsiC },
        //{ PaperSize.Kai16, AspectRatio.Kai16 },
        //{ PaperSize.Kai32, AspectRatio.Kai32 },
        { PaperSize.Legal, AspectRatio.Legal },
    };

    public static double? ToValue(this AspectRatio aspectRatio)
    {
        switch (aspectRatio)
        {
            case AspectRatio.Custom:
                return null;
            case AspectRatio.Square:
                return 1;
            case AspectRatio.ThreeByTwo:
                return 1.5;
            case AspectRatio.FourByThree:
                return 1.3333;
            case AspectRatio.A:
                return 0.7070;
            case AspectRatio.AnsiA:
                return 0.7741;
            case AspectRatio.AnsiB:
                return 0.6458;
            case AspectRatio.AnsiC:
                return 0.7728;
            //case AspectRatio.Kai8:
            //    return 0.6929;
            case AspectRatio.Kai16:
                return 0.7216;
            case AspectRatio.Kai32:
                return 0.6954;
            case AspectRatio.Legal:
                return 0.7742;
            default:
                throw new ArgumentException($"Can't convert AspectRatio {aspectRatio} to value");
        }
    }

    public static PrintMediaSize ToPrintMediaSize(this PaperSize paperSize)
    {
        return paperSize switch
        {
            PaperSize.A3 => PrintMediaSize.IsoA3,
            PaperSize.A4 => PrintMediaSize.IsoA4,
            PaperSize.A5 => PrintMediaSize.IsoA5,
            PaperSize.AnsiA => PrintMediaSize.NorthAmericaLetter,
            PaperSize.AnsiB => PrintMediaSize.NorthAmericaTabloid,
            PaperSize.AnsiC => PrintMediaSize.NorthAmericaCSheet,
            PaperSize.Legal => PrintMediaSize.NorthAmericaLegal,
            _ => throw new ArgumentException($"Can't convert {paperSize} to PrintMediaSize"),
        };
    }

    public static Rect ToRect(this PaperSize paperSize)
    {
        if (PaperSizes.TryGetValue(paperSize, out Rect rect))
            return rect;

        throw new ArgumentException($"Can't convert {paperSize} to Rect");
    }

    public static AspectRatio ToAspectRatio(this PaperSize paperSize)
    {
        if (PaperSizeAspectRatios.TryGetValue(paperSize, out AspectRatio aspectRatio))
            return aspectRatio;

        throw new ArgumentException($"Can't convert {paperSize} to AspectRatio");
    }

    public static string ToDimensionsString(this PaperSize paperSize)
    {
        Rect rect = paperSize.ToRect();
        return $"{Measurement.FromCentimeters(rect.Width / 10)} × {Measurement.FromCentimeters(rect.Height / 10)}";
    }
}
