﻿using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Windows.UI.Xaml;

namespace Scanner.Services
{
    internal class AccessibilityService : IAccessibilityService
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

        private FlowDirection _DefaultFlowDirection;
        public FlowDirection DefaultFlowDirection
        {
            get => _DefaultFlowDirection;
        }

        private FlowDirection _InvertedFlowDirection;
        public FlowDirection InvertedFlowDirection
        {
            get => _InvertedFlowDirection;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public AccessibilityService()
        {
            // get text direction
            var flowDirectionSetting = Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];
            if (flowDirectionSetting == "LTR")
            {
                _DefaultFlowDirection = FlowDirection.LeftToRight;
                _InvertedFlowDirection = FlowDirection.RightToLeft;
            }
            else
            {
                _DefaultFlowDirection = FlowDirection.RightToLeft;
                _InvertedFlowDirection = FlowDirection.LeftToRight;
            }

            LogService?.Log.Information("System text direction is {0}.", flowDirectionSetting);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    }
}
