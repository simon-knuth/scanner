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
using System.ComponentModel;

namespace Scanner.Services.Interfaces
{
    /// <summary>
    ///     Manages the current <see cref="Project"/>.
    /// </summary>
    public interface IProjectService : INotifyPropertyChanged
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Events
        event EventHandler<Project> ProjectChanged;
        event EventHandler<bool> IsProcessRunningChanged;
        event EventHandler<bool> IsScanProcessRunningChanged;
        #endregion

        Project CurrentProject { get; }
        IProjectPage SelectedPage { get; set; }

        bool IsProcessRunning { get; }
        bool IsScanProcessRunning { get; }



        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        Task TryCreateProjectAsync(ScanOptions scanOptions);
    }
}
