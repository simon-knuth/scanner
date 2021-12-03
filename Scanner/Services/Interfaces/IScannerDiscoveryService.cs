using Scanner.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Scanner.Services
{
    /// <summary>
    ///     Searches for and lists discovered wired/wireless scanners.
    /// </summary>
    public interface IScannerDiscoveryService
    {
        event EventHandler InitialCrawlCompleted;

        Task RestartSearchAsync();
        void PauseSearchAsync();
        void ResumeSearchAsync();
        Task AddDebugScannerAsync(DiscoveredScanner scanner);
        ObservableCollection<DiscoveredScanner> DiscoveredScanners
        {
            get;
        }
    }
}
