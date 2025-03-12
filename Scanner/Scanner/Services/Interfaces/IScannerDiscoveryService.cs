using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Scanner.Models.Interfaces;
using Scanner.Models;
using Scanner.Models.ScanningDevices;

namespace Scanner.Services.Interfaces
{
    /// <summary>
    ///     Searches for and lists discovered <see cref="IScanningDevice"/>s.
    /// </summary>
    public interface IScannerDiscoveryService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Events
        event EventHandler InitialCrawlCompleted;
        event EventHandler<IScanningDevice> ScanningDeviceFound;
        event EventHandler<IScanningDevice> ScanningDeviceLost;
        #endregion

        TaskCompletionSource InitialCrawlCompletion { get;  }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        Task<List<IScanningDevice>> GetScanningDevicesAsync();
        Task InitializeSearchAsync();
        Task AddDebugScannerAsync(DebugScanner scanner);
        Task RemoveDebugScannerAsync(DebugScanner scanner);
    }
}
