using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace Scanner.Services
{
    /// <summary>
    ///     Offers helper methods.
    /// </summary>
    public interface IHelperService
    {
        Task ShowRatingDialogAsync();
        Task<string> MoveFileToFolderAsync(StorageFile file, StorageFolder targetFolder, string desiredName, bool replaceExisting);
        Task<BitmapImage> GenerateBitmapFromFileAsync(StorageFile file);
    }
}
