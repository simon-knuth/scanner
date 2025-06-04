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
using static Scanner.Models.ImagePage;

namespace Scanner.Models
{
    public partial class ApplyFilterAction : IProjectAction
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        #region Services
        private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private List<ImagePage> pages;
        private ImageFilter filter;

        private Dictionary<ImagePage, ImageFilter>? previousFilters;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Applies a filter to a set of pages.
        /// </summary>
        /// <param name="pages">
        /// The pages to apply the <paramref name="filter"/> to.
        /// </param>
        /// <param name="filter">
        /// The filter to apply.
        /// </param>
        public ApplyFilterAction(List<ImagePage> pages, ImageFilter filter)
        {
            this.pages = pages;
            this.filter = filter;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<bool> ExecuteAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            bool performedChanges = false;

            previousFilters = new();
            foreach (ImagePage page in pages)
            {
                if (page.Filter != filter)
                {
                    previousFilters.Add(page, page.Filter);
                    performedChanges = true;
                }
            }

            await project.ApplyFilterToPagesAsync(pages, filter);
            return performedChanges;
        }

        public async Task UndoAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            if (previousFilters == null)
            {
                throw new ProjectException("Can't undo ApplyFilterAction without list of previous filters");
            }

            foreach (KeyValuePair<ImagePage, ImageFilter> pair in previousFilters)
            {
                await project.ApplyFilterToPagesAsync([pair.Key], pair.Value);
            }
        }

        public string GetFriendlyName()
        {
            return nameof(ApplyFilterAction);
        }
    }
}
