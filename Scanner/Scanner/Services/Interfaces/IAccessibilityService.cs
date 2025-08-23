using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Graphics.Imaging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Models.ScanningDevices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using WinRT.Interop;

namespace Scanner.Services.Interfaces
{
    /// <summary>
    ///     Simplifies meeting accessibility requirements.
    /// </summary>
    public interface IAccessibilityService
    {
        FlowDirection DefaultFlowDirection { get; }

        FlowDirection InvertedFlowDirection { get; }

        Task InitializeForLanguageTagAsync(DispatcherQueue uiDispatcherQueue, string languageTag);
    }
}
