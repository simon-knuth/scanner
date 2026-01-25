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
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Devices.Scanners;
using Windows.Storage;
using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Scanner.Views.Settings
{
    public partial class SettingsPage : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public event EventHandler<(Type, object?)>? PageNavigationRequested;
        public event EventHandler? GoBackRequested;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        protected void OnPageNavigationRequested(Type type)
        {
            OnPageNavigationRequested(type, null);
        }

        protected void OnPageNavigationRequested(Type type, object? parameter)
        {
            PageNavigationRequested?.Invoke(this, (type, parameter));
        }

        protected void OnGoBackRequested()
        {
            GoBackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
