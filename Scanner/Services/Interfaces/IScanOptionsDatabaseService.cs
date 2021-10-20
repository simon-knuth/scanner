using Scanner.Models;

namespace Scanner.Services
{
    /// <summary>
    ///     Manages remembered <see cref="ScanOptions"/> on a per-scanner basis.
    /// </summary>
    public interface IScanOptionsDatabaseService
    {
        ScanOptions GetScanOptionsForScanner(DiscoveredScanner scanner);
        void SaveScanOptionsForScanner(DiscoveredScanner scanner, ScanOptions scanOptions);
        
        /// <summary>
        ///     Deletes all remembered scan options for a given <paramref name="scanner"/>. This
        ///     can be used when the data is suspected to have been corrupted or when a scanner
        ///     with different capabilities has the same ID as a previous one (quite unlikely).
        /// </summary>
        void DeleteScanOptionsForScanner(DiscoveredScanner scanner);
    }
}
