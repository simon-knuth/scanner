using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Graphics.Imaging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
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
    ///     Exposes the Windows Copilot Runtime.
    /// </summary>
    public interface ICopilotRuntimeService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Whether the Copilot Runtime is supported on this PC.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Whether the models required to use the Windows Copilot Runtime are installed.
        /// </summary>
        bool AreModelsInstalled { get; }

        /// <summary>
        /// Whether the models required to use the Windows Copilot Runtime are currently being installed.
        /// </summary>
        bool AreModelsInstalling { get; }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        Task TryInstallModelsAsync();
        Task TryShowModelsInstallProgressAsync(DispatcherQueue uiDispatcherQueue);

        Task PreheatFileNameGenerationModelsAsync();
        Task StopPreheatingFileNameGenerationModelsAsync();
        Task<string?> TryGenerateFileNameForImageAsync(SoftwareBitmap bitmap, CancellationTokenSource cts);
    }
}
