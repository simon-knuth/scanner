using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Globalization;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using Serilog;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;
using Serilog.Sinks.File;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Storage;
using WinRT.Interop;
using static Scanner.Extensions.DispatcherQueueExtensions;

namespace Scanner.Services
{
    internal partial class AccessibilityService : ObservableObject, IAccessibilityService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        [ObservableProperty]
        private FlowDirection defaultFlowDirection = FlowDirection.LeftToRight;

        [ObservableProperty]
        private FlowDirection invertedFlowDirection = FlowDirection.RightToLeft;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public AccessibilityService()
        {

        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public async Task InitializeForLanguageTagAsync(DispatcherQueue uiDispatcherQueue, string languageTag)
        {
            await uiDispatcherQueue.RunOnThreadAndWaitAsync(DispatcherQueuePriority.High, () =>
            {
                if (CultureInfo.CreateSpecificCulture(languageTag).TextInfo.IsRightToLeft)
                {
                    DefaultFlowDirection = FlowDirection.RightToLeft;
                    InvertedFlowDirection = FlowDirection.LeftToRight;
                }
            });
            LogService?.Log.Information("System text direction is {0}", DefaultFlowDirection);
        }
    }
}
