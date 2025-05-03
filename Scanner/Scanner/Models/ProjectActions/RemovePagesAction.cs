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
using Microsoft.UI.Dispatching;

namespace Scanner.Models
{
    public partial class RemovePagesAction : IProjectAction
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        #region Services
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private List<IProjectPage> removals;

        private List<IProjectPage>? removedPages;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Removes a set of pages from a <see cref="Project"/>.
        /// </summary>
        /// <param name="removals">
        /// A list of pages to remove.
        /// </param>
        public RemovePagesAction(List<IProjectPage> removals)
        {
            this.removals = removals;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<bool> ExecuteAsync(Project project, DispatcherQueue uiDispatcherQueue)
        {
            await project.RemovePagesAsync(removals, false);
            removedPages = removals;

            return removedPages != null && removedPages.Count > 0;
        }

        public async Task UndoAsync(Project project, DispatcherQueue uiDispatcherQueue)
        {
            if (removedPages == null)
            {
                throw new ProjectException("Can't undo RemovePagesAction without list of removed pages");
            }

            await project.AddPagesAsync(removedPages);
            removedPages = null;
        }

        public string GetFriendlyName()
        {
            return nameof(RemovePagesAction);
        }
    }
}
