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
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Dispatching;

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
        Project? CurrentProject { get; }
        IProjectPage? SelectedPage { get; set; }

        bool IsProcessRunning { get; }          // scan or edit in progres
        bool IsScanProcessRunning { get; }      // scan in progress

        ScanState CurrentScanState { get; }

        bool CanSelectPreviousPage { get; }
        bool CanSelectNextPage { get; }

        Stack<IProjectAction> UndoStack { get; }
        Stack<IProjectAction> RedoStack { get; }
        bool CanUndo { get; }
        bool CanRedo { get; }

        DispatcherQueue? UiDispatcherQueue { get; set; }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        Task ApplyActionAsync(IProjectAction action);
        Task TryUndoAsync(IProjectAction? upUntil = null);
        Task TryRedoAsync(IProjectAction? upUntil = null);
        
        Task TryCreateProjectAsync(ScanOptions scanOptions);
        Task TryScanToProjectAsync(ScanOptions scanOptions);

        Task<bool> TrySaveProjectAsync();
        Task<bool> TryCloseProjectAsync(bool ignoreUnsavedChanges = false);

        void SelectPreviousPage();
        void SelectNextPage();
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public enum ScanState
    {
        Scanning,
        AutomaticRotation,
        GeneratingPDF,
        Processing,
        Saving
    }
}
