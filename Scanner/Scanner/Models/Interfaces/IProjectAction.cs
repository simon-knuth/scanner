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
using Scanner.Services.Interfaces;

namespace Scanner.Models.Interfaces;

public interface IProjectAction
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Executes the action.
    /// </summary>
    /// <returns>
    /// Whether any changes were made.
    /// </returns>
    /// <exception cref="ActionFailedAndRolledBackException">
    /// Occurs when the action failed but changes to the <see cref="ProjectBase"/> could be rolled back.
    /// </exception>
    /// <exception cref="Exception">
    /// Occurs when a fatal error occurred and the changes to the <see cref="ProjectBase"/> could not be rolled back.
    /// </exception>
    Task<bool> ExecuteAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue);

    /// <summary>
    /// Undoes the action after <see cref="ExecuteAsync(ProjectBase, DispatcherQueue)"/> has been run.
    /// Once this method has been run, <see cref="ExecuteAsync(ProjectBase, DispatcherQueue)"/> can be run again.
    /// </summary>
    /// <exception cref="ActionFailedAndRolledBackException">
    /// Occurs when the action failed but changes to the <see cref="ProjectBase"/> could be rolled back.
    /// </exception>
    /// <exception cref="Exception">
    /// Occurs when a fatal error occurred and the changes to the <see cref="ProjectBase"/> could not be rolled back.
    /// </exception>
    Task UndoAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue);

    /// <summary>
    /// Gets the friendly name of the action.
    /// </summary>
    string GetFriendlyName();

    /// <summary>
    /// Gets the analytics event (and optional properties) to report after the action has been
    /// successfully applied via <see cref="ExecuteAsync(ProjectBase, DispatcherQueue)"/>.
    /// Returns <see langword="null"/> for actions that are not tracked.
    /// Only reported for genuine user actions; the project service suppresses it when redoing.
    /// </summary>
    (AnalyticsEvent Event, Dictionary<string, string>? Properties)? GetAnalyticsEvent() => null;
}
