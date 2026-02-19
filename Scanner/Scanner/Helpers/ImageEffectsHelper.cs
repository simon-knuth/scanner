using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;
using Scanner.Models;
using System;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Services.Store;
using Windows.System;
using WinRT.Interop;

namespace Scanner.Helpers;

public static class ImageEffectsHelper
{
    /// <summary>
    /// Creates a composite effect chain with filter, brightness, and contrast applied.
    /// </summary>
    /// <param name="source">The source image to apply effects to.</param>
    /// <param name="filter">The image filter to apply.</param>
    /// <param name="brightness">Brightness adjustment (-100 to 100).</param>
    /// <param name="contrast">Contrast adjustment (-100 to 100).</param>
    /// <returns>The final effect chain ready for rendering.</returns>
    public static ICanvasImage CreateEffectChain(ICanvasImage source, ImageFilter filter, int brightness, int contrast)
    {
        ICanvasImage currentEffect = source;

        // filter
        currentEffect = ApplyFilter(currentEffect, filter);

        // brightness and contrast
        currentEffect = ApplyBrightnessAndContrast(currentEffect, brightness, contrast);

        return currentEffect;
    }

    private static ICanvasImage ApplyFilter(ICanvasImage source, ImageFilter filter)
    {
        switch (filter)
        {
            case ImageFilter.None:
                return source;

            case ImageFilter.Grayscale:
                return new GrayscaleEffect
                {
                    Source = source
                };

            case ImageFilter.Monochrome:
                var grayscale = new GrayscaleEffect
                {
                    Source = source
                };
                return new DiscreteTransferEffect
                {
                    Source = grayscale,
                    RedTable = [0.0f, 1.0f],
                    GreenTable = [0.0f, 1.0f],
                    BlueTable = [0.0f, 1.0f]
                };

            default:
                throw new ArgumentException($"Unknown filter type: {filter}");
        }
    }

    private static ICanvasImage ApplyBrightnessAndContrast(ICanvasImage source, int brightness, int contrast)
    {
        if (brightness == 0 && contrast == 0)
            return source;

        ICanvasImage currentEffect = source;

        // brightness
        if (brightness != 0)
        {
            float brightnessValue = brightness / 100.0f * 0.5f;

            currentEffect = new ColorMatrixEffect
            {
                Source = currentEffect,
                ColorMatrix = new Matrix5x4
                {
                    M11 = 1,
                    M12 = 0,
                    M13 = 0,
                    M14 = 0,
                    M21 = 0,
                    M22 = 1,
                    M23 = 0,
                    M24 = 0,
                    M31 = 0,
                    M32 = 0,
                    M33 = 1,
                    M34 = 0,
                    M41 = 0,
                    M42 = 0,
                    M43 = 0,
                    M44 = 1,
                    M51 = brightnessValue,
                    M52 = brightnessValue,
                    M53 = brightnessValue,
                    M54 = 0
                }
            };
        }

        // contrast
        if (contrast != 0)
        {
            float contrastValue = contrast / 100.0f;
            float slope = 1.0f + contrastValue;
            float offset = -contrastValue * 0.5f;

            currentEffect = new LinearTransferEffect
            {
                Source = currentEffect,
                RedSlope = slope,
                RedOffset = offset,
                GreenSlope = slope,
                GreenOffset = offset,
                BlueSlope = slope,
                BlueOffset = offset
            };
        }

        return currentEffect;
    }
}