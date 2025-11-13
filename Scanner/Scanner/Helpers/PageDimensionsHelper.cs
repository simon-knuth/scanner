using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Services.Store;
using Windows.System;
using WinRT.Interop;

namespace Scanner.Helpers
{
    public static class PageDimensionsHelper
    {
        /// <summary>
        /// Maps <see cref="PaperSize"/>s to their corresponding dimensions in mm.
        /// </summary>
        public static readonly Dictionary<PaperSize, Rect> PaperSizes = new()
        {
            { PaperSize.DinA3, new(0 , 0, 297, 320) },
            { PaperSize.DinA4, new(0 , 0, 210, 297) },
            { PaperSize.DinA5, new(0 , 0, 148, 210) },
            { PaperSize.AnsiA, new(0 , 0, 216, 279) },
            { PaperSize.AnsiB, new(0 , 0, 279, 432) },
            { PaperSize.AnsiC, new(0 , 0, 432, 559) },
            { PaperSize.Kai8, new(0 , 0, 260, 370) },
            { PaperSize.Kai16, new(0 , 0, 185, 260) },
            { PaperSize.Kai32, new(0 , 0, 130, 185) },
            { PaperSize.Letter, new(0 , 0, 216, 279) },
            { PaperSize.Legal, new(0 , 0, 216, 356) },
        };

        /// <summary>
        /// Maps <see cref="PaperSize"/>s to their corresponding <see cref="AspectRatio"/>.
        /// </summary>
        public static readonly Dictionary<PaperSize, AspectRatio> PaperSizeAspectRatios = new()
        {
            { PaperSize.DinA3, AspectRatio.DinA },
            { PaperSize.DinA4, AspectRatio.DinA },
            { PaperSize.DinA5, AspectRatio.DinA },
            { PaperSize.AnsiA, AspectRatio.AnsiA },
            { PaperSize.AnsiB, AspectRatio.AnsiB },
            { PaperSize.AnsiC, AspectRatio.AnsiC },
            { PaperSize.Kai8, AspectRatio.Kai8 },
            { PaperSize.Kai16, AspectRatio.Kai16 },
            { PaperSize.Kai32, AspectRatio.Kai32 },
            { PaperSize.Legal, AspectRatio.Legal },
        };
    }

    public enum PaperSize
    {
        Custom = 0,
        DinA3 = 1,
        DinA4 = 2,
        DinA5 = 3,
        AnsiA = 5,
        AnsiB = 6,
        AnsiC = 7,
        Kai8 = 8,
        Kai16 = 9,
        Kai32 = 10,
        Letter = 11,
        Legal = 12,
    }

    public enum AspectRatio
    {
        Custom = 0,
        Square = 1,
        ThreeByTwo = 2,
        FourByThree = 3,
        DinA = 4,
        AnsiA = 5,          // aka Letter
        AnsiB = 6,          // aka Ledger/Tabloid
        AnsiC = 7,
        Kai8 = 9,
        Kai16 = 10,
        Kai32 = 11,
        Legal = 13,
    }
}