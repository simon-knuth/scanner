using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.Services
{
    /// <summary>
    ///     Manages the app's internal storage.
    /// </summary>
    public interface IAppDataService
    {
        StorageFolder FolderTemp
        {
            get;
        }
        
        StorageFolder FolderReceivedPages
        {
            get;
        }

        StorageFolder FolderConversion
        {
            get;
        }

        StorageFolder FolderWithoutRotation
        {
            get;
        }

        Task Initialize();
        Task EmptyReceivedPagesFolderAsync();
    }
}
