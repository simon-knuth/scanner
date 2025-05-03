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
    public partial class RenameAction : IProjectAction
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        #region Services
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private IProjectPage? page;
        private string newName;

        private string? oldName;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Renames a page.
        /// </summary>
        public RenameAction(IProjectPage? page, string newName)
        {
            this.page = page;
            this.newName = newName;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<bool> ExecuteAsync(Project project, DispatcherQueue uiDispatcherQueue)
        {
            if (project.IsPdf)
            {
                oldName = project.FileNameInfo!.DesiredName;
                await project.FileNameInfo!.UpdateNamesAsync(newName, project.FileNameInfo.ActualName, uiDispatcherQueue);
            }
            else if (page is ImagePage imagePage)
            {
                oldName = imagePage.FileNameInfo.DesiredName;
                await imagePage.FileNameInfo.UpdateNamesAsync(newName, imagePage.FileNameInfo.ActualName, uiDispatcherQueue);
            }

            return true;
        }

        public async Task UndoAsync(Project project, DispatcherQueue uiDispatcherQueue)
        {
            if (oldName == null)
            {
                throw new ProjectException("Can't undo RenameAction without old name");
            }

            if (project.IsPdf)
            {
                await project.FileNameInfo!.UpdateNamesAsync(oldName, project.FileNameInfo.ActualName, uiDispatcherQueue);
            }
            else if (page is ImagePage imagePage)
            {
                await imagePage.FileNameInfo.UpdateNamesAsync(oldName, imagePage.FileNameInfo.ActualName, uiDispatcherQueue);
            }
        }

        public string GetFriendlyName()
        {
            return nameof(RenameAction);
        }
    }
}
