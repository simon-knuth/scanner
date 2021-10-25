using Microsoft.Toolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Globalization;
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
        private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();

        private const int MinimumNumberOfWords = 50;

        private OcrEngine OcrEngine;

        public IReadOnlyList<Language> AvailableLanguages => OcrEngine.AvailableRecognizerLanguages;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public AutoRotatorService()
        {
            Initialize();

            SettingsService.SettingChanged += SettingsService_SettingChanged;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Initialize()
        {
            string desiredLanguageScript = (string)SettingsService.GetSetting(AppSetting.SettingAutoRotateLanguage);

            try
            {
                Language desiredLanguage = new Language(desiredLanguageScript);
                OcrEngine = OcrEngine.TryCreateFromLanguage(desiredLanguage);
            }
            catch (Exception)
            {
                // language unavailable, reset to default
                OcrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
                SettingsService.SetSetting(AppSetting.SettingAutoRotateLanguage, OcrEngine.RecognizerLanguage.LanguageTag);
            }
        }
        
        public async Task<BitmapRotation> TryGetRecommendedRotationAsync(StorageFile imageFile, ImageScannerFormat format)
        {
            try
            {
                if (OcrEngine != null)
                {
                    // get separate stream
                    using (IRandomAccessStream sourceStream = await imageFile.OpenAsync(FileAccessMode.Read))
                    {
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream);
                        Tuple<BitmapRotation, int> bestRotation;

                        // create rotated 0°
                        SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync();
                        OcrResult ocrResult = await OcrEngine.RecognizeAsync(bitmap);
                        bestRotation = new Tuple<BitmapRotation, int>(BitmapRotation.None, ocrResult.Text.Length);

                        using (InMemoryRandomAccessStream targetStream = new InMemoryRandomAccessStream())
                        {
                            // create rotated 90°
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
                        }

                        using (InMemoryRandomAccessStream targetStream = new InMemoryRandomAccessStream())
                        {
                            // create rotated 180°
                            var encoder = await BitmapEncoder.CreateAsync(GetBitmapEncoderId(format), targetStream);
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
                        }

                        using (InMemoryRandomAccessStream targetStream = new InMemoryRandomAccessStream())
                        {
                            // create rotated 270°
                            var encoder = await BitmapEncoder.CreateAsync(GetBitmapEncoderId(format), targetStream);
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

        private void SettingsService_SettingChanged(object sender, AppSetting e)
        {
            if (e == AppSetting.SettingAutoRotateLanguage)
            {
                // desired language changed, reinitialize recognizer
                Initialize();
            }
        }
    }
}
