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
        
    }

    public enum PaperSize
    {
        Custom = 0,
        DinA3 = 1,
        DinA4 = 2,
        DinA5 = 3,
        AnsiA = 4,
        AnsiB = 5,
        AnsiC = 6,
        Legal = 9,
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
        //Kai4 = 8,
        //Kai8 = 9,
        Kai16 = 10,
        Kai32 = 11,
        Legal = 12,
    }
}