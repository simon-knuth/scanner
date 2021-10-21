using System.Threading.Tasks;
using Windows.Devices.Scanners;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Scanner.Services
{
    public interface IAutoRotatorService
    {
        Task<BitmapRotation> TryGetRecommendedRotationAsync(StorageFile imageFile, ImageScannerFormat format);
    }
}
