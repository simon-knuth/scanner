using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Scanner.Extensions;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using WinRT.Interop;
using static Scanner.Helpers.RotationHelpers;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models
{
    public partial class CropPagesAction : IProjectAction
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        #region Services
        private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private List<IProjectPage> pages;
        private Rect cropRegion;

        private List<AppliedCrop>? appliedCrops;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Crops a set of pages.
        /// </summary>
        /// <param name="pages">
        /// A list of pages to crop.
        /// </param>
        /// <param name="cropRegion">
        /// The crop to apply to all pages.
        /// </param>
        public CropPagesAction(List<IProjectPage> pages, Rect cropRegion)
        {
            this.pages = pages;
            this.cropRegion = cropRegion;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<bool> ExecuteAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            appliedCrops = await project.CropPagesAsync(pages, cropRegion, AppDataService.ChangesFolder, uiDispatcherQueue);

            return appliedCrops.Count > 0;
        }

        public async Task UndoAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            if (appliedCrops == null)
            {
                throw new ActionFailedAndRolledBackException("Can't undo CropPagesAction without list of applied crops");
            }

            // replace with pre-crop files
            foreach (AppliedCrop appliedCrop in appliedCrops)
            {
                StorageFile croppedFile = appliedCrop.Page.SourceFile;
                await appliedCrop.PreviousFile.MoveAsync(AppDataService.ChangesFolder, appliedCrop.PreviousFile.Name, NameCollisionOption.GenerateUniqueName);

                await appliedCrop.Page.ChangeSourceFileAsync(AppDataService.ChangesFolder, appliedCrop.PreviousFile, uiDispatcherQueue);

                appliedCrop.Page.Width = appliedCrop.PreviousWidth;
                appliedCrop.Page.Height = appliedCrop.PreviousHeight;

                await croppedFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }

        public string GetFriendlyName()
        {
            if (pages.Count >= 2)
                return string.Format(GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ProjectActionCropPages), pages.Count);
            else
                return GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ProjectActionCropPage);
        }
    }
}
