using Windows.Storage;

namespace Scanner.Services
{
    public interface IAppDataService
    {
        StorageFolder FolderReceivedPages
        {
            get;
        }

        StorageFolder FolderConversion
        {
            get;
        }
    }
}
