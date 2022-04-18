namespace Scanner.Models.FileNaming
{
    public interface IFileNamingBlock
    {
        string Glyph
        {
            get;
        }

        string Name
        {
            get;
        }

        string DisplayName
        {
            get;
        }


        string ToString(ScanOptions scanOptions, DiscoveredScanner scanner);
        string GetSerialized();
    }
}
