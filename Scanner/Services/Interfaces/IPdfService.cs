using Scanner.Models;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Devices.Scanners;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace Scanner.Services
{
    public interface IPdfService
    {
        event EventHandler<bool> GenerationEnded;

        Task<StorageFile> GeneratePdfAsync(string name, StorageFolder targetFolder);
    }
}
