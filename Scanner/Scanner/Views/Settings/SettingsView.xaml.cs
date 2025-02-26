using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
            ApplySettingsPage(ViewModel.SelectedPage);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ApplySettingsPage(SettingsPage page)
        {
            switch (page.PageType)
            {
                case SettingsPageType.General:
                    if (FrameContent.Content is not SettingsViewGeneral)
                    {
                        FrameContent.Navigate(typeof(SettingsViewGeneral), ViewModel);
                    }
                    break;
                case SettingsPageType.Personalization:
                    if (FrameContent.Content is not SettingsViewPersonalization)
                    {
                        FrameContent.Navigate(typeof(SettingsViewPersonalization), ViewModel);
                    }
                    break;
                case SettingsPageType.Privacy:
                    if (FrameContent.Content is not SettingsViewPrivacy)
                    {
                        FrameContent.Navigate(typeof(SettingsViewPrivacy), ViewModel);
                    }
                    break;
                case SettingsPageType.Feedback:
                    if (FrameContent.Content is not SettingsViewFeedback)
                    {
                        FrameContent.Navigate(typeof(SettingsViewFeedback), ViewModel);
                    }
                    break;
                case SettingsPageType.About:
                    if (FrameContent.Content is not SettingsViewAbout)
                    {
                        FrameContent.Navigate(typeof(SettingsViewAbout), ViewModel);
                    }
                    break;
            }
        }
        
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.SelectedPage):
                    ApplySettingsPage(ViewModel.SelectedPage);
                    break;
            }
        }
    }
}
