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
using System.Globalization;
using Windows.Storage;
using Serilog;

namespace Scanner.Services.Interfaces
{
    /// <summary>
    ///     Manages the app's internal storage.
    /// </summary>
    public interface IAppDataService
    {
        StorageFolder TempFolder
        {
            get;
        }

        /// <summary>
        /// Holds the raw files that are ready to be saved to their target.
        /// </summary>
        StorageFolder ProjectFolder
        {
            get;
        }

        /// <summary>
        /// Holds raw files that need to be added to the project.
        /// </summary>
        StorageFolder IncomingFolder
        {
            get;
        }

        /// <summary>
        /// Holds updated files that need to be reintegrated the next time the project is saved.
        /// </summary>
        StorageFolder ChangesFolder
        {
            get;
        }

        /// <summary>
        /// Contains the generated PDf file.
        /// </summary>
        StorageFolder PdfOutputFolder
        {
            get;
        }

        /// <summary>
        /// Holds prior file states for undo.
        /// </summary>
        StorageFolder UndoFolder
        {
            get;
        }

        /// <summary>
        /// Holds prior file states for redo.
        /// </summary>
        StorageFolder RedoFolder
        {
            get;
        }

        Task InitializeAsync();
        Task EmptyFolderAsync(StorageFolder folder);
        string GetUriForAppDataFolder(StorageFolder folder, string fileName);
    }
}
