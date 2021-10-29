using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Scanner.Services
{
    public interface IAutoRotatorService
    {
        IReadOnlyList<Language> AvailableLanguages
        {
            get;
        }

        Language DefaultLanguage
        {
            get;
        }

        Task<BitmapRotation> TryGetRecommendedRotationAsync(StorageFile imageFile, ImageScannerFormat format);
    }
}
