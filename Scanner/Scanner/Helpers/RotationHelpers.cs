using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;
using Scanner.Models;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Services.Store;
using Windows.System;
using WinRT.Interop;

namespace Scanner.Helpers
{
    public static class RotationHelpers
    {
        public enum RotationIntent
        {
            Degrees90,
            Degrees180,
            Degrees270,
            Automatic
        }

        public static BitmapRotation CombineRotations(BitmapRotation rotation1, BitmapRotation rotation2)
        {
            return (BitmapRotation)(((int)rotation1 + (int)rotation2) % 4);

            throw new ApplicationException("Rotations could not be combined");
        }

        public static BitmapRotation SubtractRotations(BitmapRotation rotation1, BitmapRotation rotation2)
        {
            int rotation = (int)rotation2 - (int)rotation1;
            while (rotation < 0)
            {
                rotation += 4;
            }
            return (BitmapRotation)rotation;

            throw new ApplicationException("Rotations could not be combined");
        }

        public static BitmapRotation InvertRotation(BitmapRotation rotation)
        {
            return rotation switch
            {
                BitmapRotation.None => BitmapRotation.None,
                BitmapRotation.Clockwise90Degrees => BitmapRotation.Clockwise270Degrees,
                BitmapRotation.Clockwise180Degrees => BitmapRotation.Clockwise180Degrees,
                BitmapRotation.Clockwise270Degrees => BitmapRotation.Clockwise90Degrees,
                _ => throw new ArgumentException("Invalid rotation amount to invert", nameof(rotation)),
            };
        }

        public static RotationIntent RotationAmountToRotationIntent(BitmapRotation rotation)
        {
            return rotation switch
            {
                BitmapRotation.Clockwise90Degrees => RotationIntent.Degrees90,
                BitmapRotation.Clockwise180Degrees => RotationIntent.Degrees180,
                BitmapRotation.Clockwise270Degrees => RotationIntent.Degrees270,
                _ => throw new ArgumentException("Invalid rotation amount to convert to intent", nameof(rotation)),
            };
        }

        public static BitmapRotation RotationIntentToBitmapRotation(RotationIntent rotation)
        {
            return rotation switch
            {
                RotationIntent.Degrees90 => BitmapRotation.Clockwise90Degrees,
                RotationIntent.Degrees180 => BitmapRotation.Clockwise180Degrees,
                RotationIntent.Degrees270 => BitmapRotation.Clockwise270Degrees,
                _ => throw new ArgumentException("Invalid rotation intent to convert to amount", nameof(rotation)),
            };
        }
    }
}