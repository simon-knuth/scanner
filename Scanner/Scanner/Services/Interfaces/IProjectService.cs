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

namespace Scanner.Services.Interfaces;

/// <summary>
///     Manages the current <see cref="ProjectBase"/>.
/// </summary>
public interface IProjectService : INotifyPropertyChanged, INotifyPropertyChanging
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Events
    event EventHandler? ScanCompletedSuccessfully;
    #endregion

    ProjectBase? CurrentProject { get; }

    int TotalNumberOfPages { get; }

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
    string FriendlyCurrentScanState { get; }

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
    
    Task<bool> TryCreateProjectAsync(IProjectCreationData creationData, bool keepSourceFiles, bool isAlreadySaved, DispatcherQueue uiDispatcherQueue);
    Task<bool> TryCreateProjectFromScanAsync(ScanOptions scanOptions, DispatcherQueue uiDispatcherQueue);
    Task<bool> TryScanToProjectAsync(ScanOptions scanOptions, DispatcherQueue uiDispatcherQueue);
    Task<bool> TryOpenProjectFromFilesAsync(string[] filePaths, Guid? id, DispatcherQueue uiDispatcherQueue);
    void TryCancelScan();

    Task<bool> TryConvertProjectAsync(TargetFormat targetFormat, DispatcherQueue uiDispatcherQueue);

    Task<bool> TryDeleteProjectAsync();
    Task<bool> TrySaveProjectAsync();

    Task<bool> TryCopyProjectAsync();
    Task<bool> TryCopyPagesAsync(List<ImagePage> pages);

    Task<bool> TryOpenWithProjectAsync(AppInfo? app);
    Task<bool> TryOpenWithPageAsync(AppInfo? app, ImagePage page);

    Task<bool> TryShareProjectAsync();
    Task<bool> TrySharePagesAsync(List<ImagePage> pages);

    Task<bool> TryCloseProjectAsync(bool preserveSourceFilesInIncomingFolder = false, bool ignoreUnsavedChanges = false);

    void SelectPreviousPage();
    void SelectNextPage();
    void MakeDefaultSelection();
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
