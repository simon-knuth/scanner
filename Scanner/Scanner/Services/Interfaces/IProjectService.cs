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
using Windows.ApplicationModel;

namespace Scanner.Services.Interfaces
{
    /// <summary>
    ///     Manages the current <see cref="ProjectBase"/>.
    /// </summary>
    public interface IProjectService : INotifyPropertyChanged, INotifyPropertyChanging
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ProjectBase? CurrentProject { get; }

        /// <summary>
        /// The currently selected <see cref="IProjectPage"/>.
        /// Null if no page or multiple pages are selected.
        /// </summary>
        IProjectPage? SelectedPage { get; set; }

        /// <summary>
        /// The currently selected <see cref="IProjectPage"/>s.
        /// Null if no page or just one page is selected.
        /// </summary>
        ObservableCollection<IProjectPage>? SelectedPages { get; set; }

        int SelectedPagesCount { get; }

        bool IsActionRunning { get; }               // scan/action in progres
        bool IsProcessRunning { get; }              // scan/action/edit in progres
        bool IsProcessRunningOrEditing { get; }     // scan/action/edit in progres
        bool IsScanProcessRunning { get; }          // scan in progress
        bool IsEditing { get; set; }                // edit in progress

        ScanState CurrentScanState { get; }

        bool CanSelectPreviousPage { get; }
        bool CanSelectNextPage { get; }

        Stack<IProjectAction> UndoStack { get; }
        Stack<IProjectAction> RedoStack { get; }
        bool CanUndo { get; }
        bool CanRedo { get; }

        bool CanSaveProject { get; }
        bool CanSaveAsProject { get; }

        DispatcherQueue? UiDispatcherQueue { get; set; }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        Task ApplyActionAsync(IProjectAction action);
        Task TryUndoAsync(IProjectAction? upUntil = null);
        Task TryRedoAsync(IProjectAction? upUntil = null);
        
        Task TryCreateProjectAsync(ScanOptions scanOptions, DispatcherQueue uiDispatcherQueue);
        Task TryScanToProjectAsync(ScanOptions scanOptions);

        Task<bool> TryDeleteProjectAsync();
        Task<bool> TrySaveProjectAsync();

        Task<bool> TryCopyProjectAsync();
        Task<bool> TryCopyPagesAsync(List<IProjectPage> pages);

        Task<bool> TryOpenWithProjectAsync(AppInfo? app);
        Task<bool> TryOpenWithPageAsync(AppInfo? app, IProjectPage page);

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
