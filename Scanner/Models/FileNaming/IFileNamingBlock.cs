namespace Scanner.Models.FileNaming
{
    public interface IFileNamingBlock
    {
        string Name
        {
            get;
        }

        string FriendlyName
        {
            get;
        }


        string ToString(ScanOptions scanOptions, DiscoveredScanner scanner);
        string GetSerialized();
    }
}
