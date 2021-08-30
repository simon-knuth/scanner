using Scanner.Models;

namespace Scanner.Services
{
    public interface IScanOptionsDatabaseService
    {
        ScanOptions GetScanOptionsForScanner(DiscoveredScanner scanner);
    }
}
