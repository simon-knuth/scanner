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

namespace Scanner.Models
{
    public partial class CropPagesAsCopyAction : IProjectAction
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

        private List<IProjectPage>? addedPages;


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
        public CropPagesAsCopyAction(List<IProjectPage> pages, Rect cropRegion)
        {
            this.pages = pages;
            this.cropRegion = cropRegion;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<bool> ExecuteAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            addedPages = await project.CropPagesAsCopyAsync(pages, cropRegion, AppDataService.ChangesFolder);

            return addedPages.Count > 0;
        }

        public async Task UndoAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            if (addedPages == null)
            {
                throw new ActionFailedAndRolledBackException("Can't undo CropPagesAsCopyAction without list of applied crops");
            }

            // remove added pages
            await project.RemovePagesAsync(addedPages, false);
        }

        public string GetFriendlyName()
        {
            return nameof(CropPagesAction);
        }
    }
}
