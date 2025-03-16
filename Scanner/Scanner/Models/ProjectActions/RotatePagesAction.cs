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
        public async Task ExecuteAsync(Project project, DispatcherQueue uiDispatcherQueue)
        {
            // convert RotationIntent to BitmapRotation
            Dictionary<IProjectPage, BitmapRotation> rotations = new();
            foreach (KeyValuePair<IProjectPage, RotationIntent> rotation in this.rotations)
            {
                rotations.Add(rotation.Key, RotationIntentToBitmapRotation(rotation.Value));
            }

            await project.RotatePagesAsync(rotations);
        }

        public async Task UndoAsync(Project project)
        {
            if (appliedRotations == null)
            {
                throw new ProjectException("Can't undo RotatePagesAction without list of applied rotations");
            }

            Dictionary<IProjectPage, BitmapRotation> invertedRotations = new();
            foreach (KeyValuePair<IProjectPage, BitmapRotation> rotation in appliedRotations)
            {
                invertedRotations.Add(rotation.Key, InvertRotation(rotation.Value));
            }

            await project.RotatePagesAsync(invertedRotations);
        }

        private BitmapRotation InvertRotation(BitmapRotation rotation)
        {
            return rotation switch
            {
                BitmapRotation.None => BitmapRotation.None,
                BitmapRotation.Clockwise90Degrees => BitmapRotation.Clockwise270Degrees,
                BitmapRotation.Clockwise180Degrees => BitmapRotation.Clockwise180Degrees,
                BitmapRotation.Clockwise270Degrees => BitmapRotation.Clockwise90Degrees,
                _ => throw new ArgumentException("Invalid rotation amount to invert", nameof(rotation)),
            };
        }

        private RotationIntent RotationAmountToRotationIntent(BitmapRotation rotation)
        {
            return rotation switch
            {
                BitmapRotation.Clockwise90Degrees => RotationIntent.Degrees90,
                BitmapRotation.Clockwise180Degrees => RotationIntent.Degrees180,
                BitmapRotation.Clockwise270Degrees => RotationIntent.Degrees270,
                _ => throw new ArgumentException("Invalid rotation amount to convert to intent", nameof(rotation)),
            };
        }

        private BitmapRotation RotationIntentToBitmapRotation(RotationIntent rotation)
        {
            return rotation switch
            {
                RotationIntent.Degrees90 => BitmapRotation.Clockwise90Degrees,
                RotationIntent.Degrees180 => BitmapRotation.Clockwise180Degrees,
                RotationIntent.Degrees270 => BitmapRotation.Clockwise270Degrees,
                _ => throw new ArgumentException("Invalid rotation intent to convert to amount", nameof(rotation)),
            };
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public enum RotationIntent
    {
        Degrees90,
        Degrees180,
        Degrees270,
        Automatic
    }
}
