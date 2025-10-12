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
using static Scanner.Helpers.Helpers;

namespace Scanner.Models
{
    public partial class ApplyOrderOfPagesAction : IProjectAction
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        #region Services
        private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private List<IProjectPage> targetOrder;

        private List<IProjectPage>? previousOrder;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Applies a specified order of pages to the project.
        /// </summary>
        /// <param name="pages">
        /// The taregt page order.
        /// </param>
        public ApplyOrderOfPagesAction(List<IProjectPage> targetOrder)
        {
            this.targetOrder = targetOrder;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<bool> ExecuteAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            previousOrder = [.. project.Pages];

            return await project.ApplyOrderOfPagesAsync(targetOrder, uiDispatcherQueue);
        }

        public async Task UndoAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            if (previousOrder == null)
            {
                throw new ActionFailedAndRolledBackException($"Can't undo {nameof(ApplyOrderOfPagesAction)} without previous order");
            }

            await project.ApplyOrderOfPagesAsync(previousOrder, uiDispatcherQueue);
        }

        public string GetFriendlyName()
        {
            return GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ProjectActionApplyOrderToPages);
        }
    }
}
