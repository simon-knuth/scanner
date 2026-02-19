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
using System.ComponentModel.DataAnnotations;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Devices.Scanners;
using Windows.Storage;
using System.ComponentModel;
using Microsoft.UI.Dispatching;

namespace Scanner.Models.Interfaces;

public interface IAtomicProjectAction : IProjectAction
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Most recent time of execution.
    /// </summary>
    DateTime MostRecentExecution { get; }

    ImagePage Page { get; }
    int TargetValue { get; }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Executes the action.
    /// </summary>
    /// <returns>
    /// Whether any changes were made.
    /// </returns>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    /// <exception cref="ActionFailedAndRolledBackException">
    /// Occurs when the action failed but changes to the <see cref="ProjectBase"/> could be rolled back.
    /// </exception>
    /// <exception cref="Exception">
    /// Occurs when a fatal error occurred and the changes to the <see cref="ProjectBase"/> could not be rolled back.
    /// </exception>
    bool Execute(ProjectBase project, DispatcherQueue uiDispatcherQueue);

    /// <summary>
    /// Execute the action using the value from <paramref name="action"/>.
    /// </summary>
    /// <returns>
    /// Whether any changes were made.
    /// </returns>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    /// <exception cref="ActionFailedAndRolledBackException">
    /// Occurs when the action failed but changes to the <see cref="ProjectBase"/> could be rolled back.
    /// </exception>
    /// <exception cref="Exception">
    /// Occurs when a fatal error occurred and the changes to the <see cref="ProjectBase"/> could not be rolled back.
    /// </exception>
    bool MergeAndExecute(ProjectBase projectBase, IAtomicProjectAction action, DispatcherQueue uiDispatcherQueue);

    /// <summary>
    /// Checks whether the given <paramref name="action"/> can be merged with this action.
    /// </summary>
    /// <param name="action">The action to merge.</param>
    bool IsActionCompatibleForMerge(IAtomicProjectAction action);
}
