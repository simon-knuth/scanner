using Scanner.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Scanner.Services
{
    public interface IScannerDiscoveryService
    {
        event EventHandler InitialCrawlCompleted;

        Task RestartSearchAsync();
        Task AddDebugScannerAsync(DiscoveredScanner scanner);
        ObservableCollection<DiscoveredScanner> DiscoveredScanners
        {
            get;
        }
    }
}
