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
using System.Security.Principal;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace Scanner.Views
{
    [ObservableObjectAttribute]
    public sealed partial class ScanActionsView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Events
        public event EventHandler ExpandScanOptionsRequested;
        #endregion

        #region Dependency Properties
        public static readonly DependencyProperty AreScanOptionsVisibleProperty =
            DependencyProperty.Register(nameof(AreScanOptionsVisible), typeof(bool), typeof(ScanActionsView), null);
        #endregion

        public bool AreScanOptionsVisible
        {
            get => (bool)GetValue(AreScanOptionsVisibleProperty);
            set
            {
                SetValue(AreScanOptionsVisibleProperty, value);
                OnPropertyChanged(nameof(IsTemplatesButtonVisible));
                OnPropertyChanged(nameof(IsPreviewButtonVisible));
            }
        }

        [ObservableProperty]
        private bool canScanModeBeSwitched = true;

        [ObservableProperty]
        private bool isAddToProject;

        public bool IsTemplatesButtonVisible => !AreScanOptionsVisible || GridRoot.ActualWidth > 400;
        public bool IsPreviewButtonVisible => !AreScanOptionsVisible || GridRoot.ActualWidth > 500;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanActionsView()
        {
            this.InitializeComponent();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ButtonScanMode_Click(object sender, RoutedEventArgs e)
        {
            IsAddToProject = !IsAddToProject;
        }

        private void ButtonScanOptions_Click(object sender, RoutedEventArgs e)
        {
            ExpandScanOptionsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void GridRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsTemplatesButtonVisible));
            OnPropertyChanged(nameof(IsPreviewButtonVisible));
        }
    }
}
