using Scanner.Models;

namespace Scanner.Services
{
    /// <summary>
    ///     Manages <see cref="PersistentScanOptions"/> on a per-scanner basis.
    /// </summary>
    public interface IPersistentScanOptionsDatabaseService
    {
        PersistentScanOptions GetPersistentScanOptionsForScanner(DiscoveredScanner scanner);
        void SavePersistentScanOptionsForScanner(DiscoveredScanner scanner, PersistentScanOptions persistentScanOptions);
        
        /// <summary>
        ///     Deletes all persistent scan options for a given <paramref name="scanner"/>. This
        ///     can be used when the data is suspected to have been corrupted or when a scanner
        ///     with different capabilities has the same ID as a previous one (quite unlikely).
        /// </summary>
        void DeletePersistentScanOptionsForScanner(DiscoveredScanner scanner);
    }
}
