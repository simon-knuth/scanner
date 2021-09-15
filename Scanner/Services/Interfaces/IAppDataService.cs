using Windows.Storage;

namespace Scanner.Services
{
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
    }
}
