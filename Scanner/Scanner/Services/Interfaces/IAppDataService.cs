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

        StorageFolder ReceivedPagesFolder
        {
            get;
        }

        StorageFolder ProjectFolder
        {
            get;
        }

        Task InitializeAsync();
        Task EmptyReceivedPagesFolderAsync();
        Task EmptyProjectFolderAsync();
        string GetUriForAppDataFolder(StorageFolder folder, string fileName);
    }
}
