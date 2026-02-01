using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Scanner.Messages;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using WinRT.Interop;
using static Scanner.Helpers.Helpers;

namespace Scanner.Models
{
    public partial class RemovePagesAction : ObservableRecipient, IProjectAction
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        #region Services
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        private static readonly IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
        #endregion

        private List<ImagePage> removals;

        private List<ImagePage>? removedPages;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Removes a set of pages from a <see cref="ProjectBase"/>.
        /// </summary>
        /// <param name="removals">
        /// A list of pages to remove.
        /// </param>
        public RemovePagesAction(List<ImagePage> removals)
        {
            this.removals = removals;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<bool> ExecuteAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            if (project.Pages.Count == removals.Count)
                return await ProjectService.TryDeleteProjectAsync();
            else
                await project.RemovePagesAsync(removals, false, uiDispatcherQueue);

            removedPages = removals;
            return removedPages != null && removedPages.Count > 0;
        }

        public async Task UndoAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            if (removedPages == null)
            {
                throw new ActionFailedAndRolledBackException("Can't undo RemovePagesAction without list of removed pages");
            }

            await project.AddPagesAsync(removedPages, uiDispatcherQueue);
            removedPages = null;
        }

        public string GetFriendlyName()
        {
            if (removals.Count >= 2)
                return string.Format(GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ProjectActionRemovePages), removals.Count);
            else
                return GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ProjectActionRemovePage);
        }
    }
}
