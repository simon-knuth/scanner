using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Scanner.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace Scanner.Views.Settings
{
    public sealed partial class SettingsView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SettingsView()
        {
            this.InitializeComponent();

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ApplySettingsPageEntry(ViewModel.SelectedPage);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ApplySettingsPageEntry(SettingsPageEntry page)
        {
            switch (page.PageType)
            {
                case SettingsPageType.General:
                    if (FrameContent.Content is not SettingsViewGeneral)
                    {
                        Navigate(typeof(SettingsViewGeneral), new EntranceNavigationTransitionInfo());
                    }
                    break;
                case SettingsPageType.Personalization:
                    if (FrameContent.Content is not SettingsViewPersonalization)
                    {
                        Navigate(typeof(SettingsViewPersonalization), new EntranceNavigationTransitionInfo());
                    }
                    break;
                case SettingsPageType.Privacy:
                    if (FrameContent.Content is not SettingsViewPrivacy)
                    {
                        Navigate(typeof(SettingsViewPrivacy), new EntranceNavigationTransitionInfo());
                    }
                    break;
                case SettingsPageType.Feedback:
                    if (FrameContent.Content is not SettingsViewFeedback)
                    {
                        Navigate(typeof(SettingsViewFeedback), new EntranceNavigationTransitionInfo());
                    }
                    break;
                case SettingsPageType.About:
                    if (FrameContent.Content is not SettingsViewAbout)
                    {
                        Navigate(typeof(SettingsViewAbout), new EntranceNavigationTransitionInfo());
                    }
                    break;
            }
        }

        private void Navigate(Type pageType, NavigationTransitionInfo transition)
        {
            FrameContent.Navigate(pageType, ViewModel, transition);
        }

        private void SettingsView_GoBackRequested(object? sender, EventArgs e)
        {
            FrameContent.GoBack();
        }

        private void SettingsView_PageNavigationRequested(object? sender, Type e)
        {
            Navigate(e, new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromRight
            });
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.SelectedPage):
                    ApplySettingsPageEntry(ViewModel.SelectedPage);
                    break;
            }
        }

        private void FrameContent_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (FrameContent.Content != null && FrameContent.Content is SettingsPage settingsPage)
            {
                settingsPage.PageNavigationRequested -= SettingsView_PageNavigationRequested;
                settingsPage.GoBackRequested -= SettingsView_GoBackRequested;
            }
        }

        private void FrameContent_Navigated(object sender, NavigationEventArgs e)
        {
            if (FrameContent.Content != null)
            {
                ((SettingsPage)FrameContent.Content).PageNavigationRequested += SettingsView_PageNavigationRequested;
                ((SettingsPage)FrameContent.Content).GoBackRequested += SettingsView_GoBackRequested;
            }
        }

        private void Page_Loading(FrameworkElement sender, object args)
        {
            // update titlebar spacing
            AppWindowTitleBar? titlebar = ((App)Application.Current).SettingsWindow?.AppWindow.TitleBar;

            if (titlebar != null)
            {
                double scaleAdjustment = this.XamlRoot.RasterizationScale;
                double headerInset = ViewModel.AccessibilityService.DefaultFlowDirection == FlowDirection.LeftToRight ? titlebar.LeftInset : titlebar.RightInset;
                double footerInset = ViewModel.AccessibilityService.DefaultFlowDirection == FlowDirection.LeftToRight ? titlebar.RightInset : titlebar.LeftInset;
                ColumnDefinitionTitlebarInsetHeader.Width = new GridLength(headerInset / scaleAdjustment);
                ColumnDefinitionTitlebarInsetFooter.Width = new GridLength(footerInset / scaleAdjustment);
            }
        }
    }
}
