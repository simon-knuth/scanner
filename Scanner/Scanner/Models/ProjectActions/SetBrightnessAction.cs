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
using static Scanner.Helpers.Helpers;

namespace Scanner.Models
{
    public partial class SetBrightnessAction : IAtomicProjectAction
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        #region Services
        private static readonly IAppDataService AppDataService = Ioc.Default.GetRequiredService<IAppDataService>();
        private static readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        public DateTime MostRecentExecution { get; private set; } = DateTime.MinValue;

        public ImagePage Page { get; private set; }
        public int TargetValue { get; private set; }

        private int? previousValue;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// Adjusts a page's brightness.
        /// </summary>
        /// <param name="page">
        /// The page to modify.
        /// </param>
        /// <param name="targetValue">
        /// The brightness value to apply.
        /// </param>
        public SetBrightnessAction(ImagePage page, int targetValue)
        {
            Page = page;
            TargetValue = targetValue;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public bool Execute(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            previousValue ??= Page.Brightness;
            project.SetBrightness(Page, TargetValue, uiDispatcherQueue);
            MostRecentExecution = DateTime.Now;
            return true;
        }

        public Task<bool> ExecuteAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            return Task.FromResult(Execute(project, uiDispatcherQueue));
        }

        public bool MergeAndExecute(ProjectBase projectBase, IAtomicProjectAction action, DispatcherQueue uiDispatcherQueue)
        {
            if (action is not SetBrightnessAction)
                throw new ArgumentException("Only actions of the same type can be merged");

            TargetValue = action.TargetValue;
            return Execute(projectBase, uiDispatcherQueue);
        }

        public async Task UndoAsync(ProjectBase project, DispatcherQueue uiDispatcherQueue)
        {
            if (previousValue == null)
                throw new ActionFailedAndRolledBackException($"Can't undo {nameof(SetBrightnessAction)} without previous value");

            project.SetBrightness(Page, (int)previousValue, uiDispatcherQueue);
        }

        public string GetFriendlyName()
        {
            return GetLocalized(Resources.Strings.ResourcesExtension.KeyEnum.ProjectActionSetBrightness);
        }
    }
}
