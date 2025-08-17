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
using static Scanner.Helpers.Helpers;

namespace Scanner.Models
{
    public partial class RotatePagesAction : IProjectAction
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        #region Services
        private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private Dictionary<IProjectPage, RotationIntent> rotations;

        private Dictionary<IProjectPage, BitmapRotation>? appliedRotations;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Rotates a set of pages.
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
        public async Task<bool> ExecuteAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            appliedRotations = await project.RotatePagesAsync(rotations, AppDataService.ChangesFolder, uiDispatcherQueue);

            return appliedRotations.Count > 0 && appliedRotations.Values.Any((x) => x != BitmapRotation.None);
        }

        public async Task UndoAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            if (appliedRotations == null)
            {
                throw new ActionFailedAndRolledBackException("Can't undo RotatePagesAction without list of applied rotations");
            }

            // gather instructions
            Dictionary<IProjectPage, BitmapRotation> invertedRotations = new();
            foreach (KeyValuePair<IProjectPage, BitmapRotation> rotation in appliedRotations)
            {
                invertedRotations.Add(rotation.Key, InvertRotation(rotation.Value));
            }

            await project.RotatePagesAsync(invertedRotations, AppDataService.ChangesFolder, uiDispatcherQueue);
        }

        public string GetFriendlyName()
        {
            if (rotations.Count >= 2)
                return string.Format(GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ProjectActionRotatePages), rotations.Count);
            else
                return GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ProjectActionRotatePage);
        }
    }
}
