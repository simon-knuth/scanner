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

namespace Scanner.Models.Interfaces
{
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
        /// <exception cref="ProjectException">
        /// Occurs when the action failed but changes to the <see cref="Project"/> could be rolled back.
        /// </exception>
        /// <exception cref="Exception">
        /// Occurs when a fatal error occurred and the changes to the <see cref="Project"/> could not be rolled back.
        /// </exception>
        Task ExecuteAsync(Project project, DispatcherQueue uiDispatcherQueue);

        /// <summary>
        /// Undoes the action after <see cref="ExecuteAsync(Project, DispatcherQueue)"/> has been run.
        /// Once this method has been run, <see cref="ExecuteAsync(Project, DispatcherQueue)"/> can be run again.
        /// </summary>
        /// <exception cref="ProjectException">
        /// Occurs when the action failed but changes to the <see cref="Project"/> could be rolled back.
        /// </exception>
        /// <exception cref="Exception">
        /// Occurs when a fatal error occurred and the changes to the <see cref="Project"/> could not be rolled back.
        /// </exception>
        Task UndoAsync(Project project);
    }
}
