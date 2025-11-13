using Microsoft.UI.Dispatching;
using Scanner.Helpers;
using Scanner.Models;
using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using static Scanner.Helpers.PageDimensionsHelper;

namespace Scanner.Extensions
{
    public static class AspectRatioExtensions
    {
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
                case AspectRatio.DinA:
                    return 0.7070;
                case AspectRatio.AnsiA:
                    return 0.7741;
                case AspectRatio.AnsiB:
                    return 0.6458;
                case AspectRatio.AnsiC:
                    return 0.7728;
                case AspectRatio.Kai8:
                    return 0.6929;
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
    }
}
