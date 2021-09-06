using Scanner.Models;

namespace Scanner.Services
{
    /// <summary>
    ///     Manages remembered <see cref="ScanOptions"/> on a per-scanner basis.
    /// </summary>
    public interface IScanOptionsDatabaseService
    {
        ScanOptions GetScanOptionsForScanner(DiscoveredScanner scanner);
    }
}
