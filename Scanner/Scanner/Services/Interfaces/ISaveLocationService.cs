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
using Windows.Graphics.Imaging;
using Windows.Storage;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Scanner.Services.Interfaces
{
    /// <summary>
    ///     Manages and exposes save locations.
    /// </summary>
    public interface ISaveLocationService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Determines the save location to use for a scan. This can result in a file picker dialog opening and even the user
        ///     cancelling the operation.
        /// </summary>
        /// <returns>A <see cref="StorageFile"/> to save to.</returns>
        Task<SaveOptions?> GetSaveOptionsAsync(DispatcherQueue uiDispatcherQueue, Window window, ScanOptions scanOptions, Project? existingProject);

        /// <summary>
        ///     Gets the currently selected fixed save location regardless of whether it's used or not. Can be null if unsupported.
        /// </summary>
        /// <returns>The <see cref="StorageFolder"/> that is used as the fixed save location.</returns>
        Task<StorageFolder?> GetFixedSaveLocationAsync();

        /// <summary>
        ///    Allows the user to select a new fixed save location.
        /// </summary>
        /// <returns>
        ///     The updated save location. This can be the same as before or null, especially if the user cancelled the operation.
        /// </returns>
        Task<StorageFolder?> SelectFixedSaveLocationAsync(DispatcherQueue uiDispatcherQueue, Window window);

        /// <summary>
        ///     Resets the save location to a default value.
        /// </summary>
        /// <returns>The updated save location. Can be null if fixed save locations aren't supported.</returns>
        Task<StorageFolder?> TryResetSaveLocationAsync();


        /// <summary>
        ///    Determines if using a fixed save location is supported.
        /// </summary>
        Task<bool> GetIsFixedSaveLocationSupportedAsync();
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public record SaveOptions(StorageFolder TargetFolder, string FileName);    
}
