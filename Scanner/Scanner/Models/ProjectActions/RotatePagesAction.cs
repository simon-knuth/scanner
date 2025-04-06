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
using Windows.Storage;
using CommunityToolkit.Mvvm.DependencyInjection;
using Scanner.Services.Interfaces;
using Scanner.Models.Interfaces;
using System.ComponentModel;
using Windows.Graphics.Imaging;
using Microsoft.UI.Dispatching;
using static Scanner.Helpers.RotationHelpers;

namespace Scanner.Models
{
    public partial class RotatePagesAction : IProjectAction
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        #region Services
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private Dictionary<IProjectPage, RotationIntent> rotations;

        private Dictionary<IProjectPage, BitmapRotation>? appliedRotations;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Rotates a set of pages <see cref="Project"/> at specific indices.
        /// </summary>
        /// <param name="rotations">
        /// A sorted list of pages to rotate, with their respective rotation amounts. Rotations are applied in the order they are listed.
        /// </param>
        public RotatePagesAction(Dictionary<IProjectPage, RotationIntent> rotations)
        {
            this.rotations = rotations;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<bool> ExecuteAsync(Project project, DispatcherQueue uiDispatcherQueue)
        {
            appliedRotations = await Project.RotatePagesAsync(rotations);

            if (appliedRotations != null && appliedRotations.Count > 0)
            {
                project.IsSaved = false;
                return true;
            }
            return false;
        }

        public async Task UndoAsync(Project project)
        {
            if (appliedRotations == null)
            {
                throw new ProjectException("Can't undo RotatePagesAction without list of applied rotations");
            }

            // gather instructions
            Dictionary<IProjectPage, BitmapRotation> invertedRotations = new();
            foreach (KeyValuePair<IProjectPage, BitmapRotation> rotation in appliedRotations)
            {
                invertedRotations.Add(rotation.Key, InvertRotation(rotation.Value));
            }

            await Project.RotatePagesAsync(invertedRotations);
        }

        public string GetFriendlyName()
        {
            return nameof(RotatePagesAction);
        }
    }
}
