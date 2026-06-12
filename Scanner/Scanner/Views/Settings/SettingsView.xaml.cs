using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Services.Interfaces;
using Scanner.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace Scanner.Views.Settings;

public sealed partial class SettingsView : Page
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public SettingsViewModel ViewModel { get; }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public SettingsView(SettingsViewModelIntent? intent = null)
    {
        ViewModel = new(intent);

        this.InitializeComponent();
        Ioc.Default.GetService<ILogService>()?.Log.Information("View loaded");

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ApplySettingsPageEntry(ViewModel.SelectedPage);
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void ApplySettingsPageEntry(SettingsPageEntry page)
    {
        NavigationTransitionInfo transitionInfo = ViewModel.SettingsService.SettingAnimations ? 
            new EntranceNavigationTransitionInfo() : new SuppressNavigationTransitionInfo();
        switch (page.PageType)
        {
            case SettingsPageType.General:
                if (FrameContent.Content is not SettingsViewGeneral)
                {
                    Navigate(typeof(SettingsViewGeneral), transitionInfo);
                }
                break;
            case SettingsPageType.Personalization:
                if (FrameContent.Content is not SettingsViewPersonalization)
                {
                    Navigate(typeof(SettingsViewPersonalization), transitionInfo);
                }
                break;
            case SettingsPageType.Privacy:
                if (FrameContent.Content is not SettingsViewPrivacy)
                {
                    Navigate(typeof(SettingsViewPrivacy), transitionInfo);
                }
                break;
            case SettingsPageType.Feedback:
                if (FrameContent.Content is not SettingsViewFeedback)
                {
                    Navigate(typeof(SettingsViewFeedback), transitionInfo);
                }
                break;
            case SettingsPageType.About:
                if (FrameContent.Content is not SettingsViewAbout)
                {
                    Navigate(typeof(SettingsViewAbout), transitionInfo);
                }
                break;
        }
    }

    private void Navigate(Type pageType, NavigationTransitionInfo transition, object? parameter = null)
    {
        FrameContent.Navigate(pageType, parameter ?? ViewModel, transition);
    }

    private void SettingsView_GoBackRequested(object? sender, EventArgs e)
    {
        FrameContent.GoBack();
    }

    private void SettingsView_PageNavigationRequested(object? sender, (Type, object?) e)
    {
        Navigate(e.Item1, ViewModel.SettingsService.SettingAnimations ? new SlideNavigationTransitionInfo
        {
            Effect = SlideNavigationTransitionEffect.FromRight
        } : new SuppressNavigationTransitionInfo(), e.Item2);
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
