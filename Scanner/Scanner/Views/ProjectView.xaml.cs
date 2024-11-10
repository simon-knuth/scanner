using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace Scanner.Views
{
    [ObservableObjectAttribute]
    public sealed partial class ProjectView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Dependency Properties
        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ProjectView), null);
        #endregion

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set
            {
                SetValue(IsExpandedProperty, value);
            }
        }

        [ObservableProperty]
        private double projectFlyoutWidth;

        private bool showEntranceExitAnimations;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ProjectView()
        {
            this.InitializeComponent();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ButtonMore_Click(object sender, RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(GridHeader);
        }

        private void GridHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ProjectFlyoutWidth = e.NewSize.Width - 24;
        }

        private void ButtonRotate_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            FlyoutBase.ShowAttachedFlyout(ButtonRotate);
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(500);
            showEntranceExitAnimations = true;
        }

        private void ControlAnimated_Loading(FrameworkElement sender, object args)
        {
            // prevent animations during application startup
            if (!showEntranceExitAnimations)
            {
                Implicit.SetShowAnimations(sender, new ImplicitAnimationSet());
                Implicit.SetHideAnimations(sender, new ImplicitAnimationSet());
            }
        }
    }
}
