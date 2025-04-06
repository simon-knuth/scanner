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
    public partial class AddFilesAction : IProjectAction
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        #region Services
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        private List<ProjectFileInsertion> insertions;
        private bool keepSourceFiles;

        private List<IProjectPage>? addedPages;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Adds a set of files to a <see cref="Project"/> at specific indices.
        /// </summary>
        /// <param name="insertions">
        /// A sorted list of files to add to the project, with their respective FINAL indices. Insertions are applied in the order they are listed.
        /// Ensure that the indices are valid when the insertion happens.
        /// </param>
        public AddFilesAction(List<ProjectFileInsertion> insertions, bool keepSourceFiles)
        {
            this.insertions = insertions;
            this.keepSourceFiles = keepSourceFiles;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task<bool> ExecuteAsync(Project project, DispatcherQueue uiDispatcherQueue)
        {
            addedPages = await project.AddFilesAsync(insertions, keepSourceFiles);
            
            return addedPages != null && addedPages.Count > 0;
        }

        public async Task UndoAsync(Project project)
        {
            if (addedPages == null)
            {
                throw new ProjectException("Can't undo AddFilesAction without list of added pages");
            }

            await project.RemovePagesAsync(addedPages, true);
            addedPages = null;
        }

        public string GetFriendlyName()
        {
            return nameof(AddFilesAction);
        }
    }
}
