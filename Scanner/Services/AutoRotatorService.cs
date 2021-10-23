using Microsoft.Toolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

using static Utilities;

namespace Scanner.Services
{
    internal class AutoRotatorService : IAutoRotatorService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        private readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();

        private const int MinimumNumberOfWords = 25;

        private OcrEngine OcrEngine;

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public AutoRotatorService()
        {
            OcrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<BitmapRotation> TryGetRecommendedRotationAsync(StorageFile imageFile, ImageScannerFormat format)
        {
            try
            {
                if (OcrEngine != null)
                {
                    // get separate stream
                    using (IRandomAccessStream sourceStream = await imageFile.OpenAsync(FileAccessMode.Read))
                    {
                        Tuple<BitmapRotation, int> bestRotation;

                        // create rotated 0°
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream);
                        SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync();
                        OcrResult ocrResult = await OcrEngine.RecognizeAsync(bitmap);
                        bestRotation = new Tuple<BitmapRotation, int>(BitmapRotation.None, ocrResult.Text.Length);

                        // check rotated 90°
                        InMemoryRandomAccessStream targetStream = new InMemoryRandomAccessStream();
                        var encoder = await BitmapEncoder.CreateAsync(GetBitmapEncoderId(format), targetStream);
                        encoder.SetSoftwareBitmap(bitmap);
                        encoder.BitmapTransform.Rotation = BitmapRotation.Clockwise90Degrees;
                        await encoder.FlushAsync();
                        decoder = await BitmapDecoder.CreateAsync(targetStream);
                        bitmap = await decoder.GetSoftwareBitmapAsync();
                        ocrResult = await OcrEngine.RecognizeAsync(bitmap);
                        if (ocrResult.Text.Length > bestRotation.Item2)
                        {
                            bestRotation = new Tuple<BitmapRotation, int>(BitmapRotation.Clockwise90Degrees, ocrResult.Text.Length);
                        }

                        // create rotated 180°
                        targetStream = new InMemoryRandomAccessStream();
                        encoder = await BitmapEncoder.CreateAsync(GetBitmapEncoderId(format), targetStream);
                        encoder.SetSoftwareBitmap(bitmap);
                        encoder.BitmapTransform.Rotation = BitmapRotation.Clockwise90Degrees;
                        await encoder.FlushAsync();
                        decoder = await BitmapDecoder.CreateAsync(targetStream);
                        bitmap = await decoder.GetSoftwareBitmapAsync();
                        ocrResult = await OcrEngine.RecognizeAsync(bitmap);
                        if (ocrResult.Text.Length > bestRotation.Item2)
                        {
                            bestRotation = new Tuple<BitmapRotation, int>(BitmapRotation.Clockwise180Degrees, ocrResult.Text.Length);
                        }

                        // create rotated 270°
                        targetStream = new InMemoryRandomAccessStream();
                        encoder = await BitmapEncoder.CreateAsync(GetBitmapEncoderId(format), targetStream);
                        encoder.SetSoftwareBitmap(bitmap);
                        encoder.BitmapTransform.Rotation = BitmapRotation.Clockwise90Degrees;
                        await encoder.FlushAsync();
                        decoder = await BitmapDecoder.CreateAsync(targetStream);
                        bitmap = await decoder.GetSoftwareBitmapAsync();
                        ocrResult = await OcrEngine.RecognizeAsync(bitmap);
                        if (ocrResult.Text.Length > bestRotation.Item2)
                        {
                            bestRotation = new Tuple<BitmapRotation, int>(BitmapRotation.Clockwise270Degrees, ocrResult.Text.Length);
                        }

                        if (bestRotation.Item2 < MinimumNumberOfWords)
                        {
                            // very low confidence, could just be random patterns
                            return BitmapRotation.None;
                        }
                        else
                        {
                            return bestRotation.Item1;
                        }
                    }
                }
                else
                {
                    return BitmapRotation.None;
                }
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Determining the recommended rotation failed.");
                AppCenterService?.TrackError(exc);
                return BitmapRotation.None;
            }            
        }
    }
}
