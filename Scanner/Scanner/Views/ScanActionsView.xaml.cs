using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Services.Interfaces;
using Scanner.Views.Flyouts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Principal;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Text;


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
        public event EventHandler AreScanOptionsVisibleChanged;
        #endregion

        #region Dependency Properties
        public static readonly DependencyProperty AreScanOptionsVisibleProperty =
            DependencyProperty.Register(nameof(AreScanOptionsVisible), typeof(bool), typeof(ScanActionsView),
                new PropertyMetadata(false, OnAreScanOptionsVisibleChanged));

        public static readonly DependencyProperty ScanOptionsProperty =
            DependencyProperty.Register(nameof(ScanOptions), typeof(ScanOptions), typeof(ScanActionsView), null);
        #endregion

        public bool AreScanOptionsVisible
        {
            get => (bool)GetValue(AreScanOptionsVisibleProperty);
            set => SetValue(AreScanOptionsVisibleProperty, value);
        }

        public ScanOptions? ScanOptions
        {
            get => ViewModel.ScanOptions;
            set
            {
                SetValue(ScanOptionsProperty, value);
                ViewModel.ScanOptions = value;
            }
        }

        [ObservableProperty]
        private bool isHoveringCancelButton;

        [ObservableProperty]
        private string scanStateDescription;

        public bool IsTemplatesButtonVisible => !AreScanOptionsVisible || GridRoot.ActualWidth > 400;
        public bool IsPreviewButtonVisible => !AreScanOptionsVisible || GridRoot.ActualWidth > 500;

        private bool showEntranceAnimations;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanActionsView()
        {
            this.InitializeComponent();

            ViewModel.ProjectService.PropertyChanged += ProjectService_PropertyChanged;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ButtonScanMode_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AddToProject = !ViewModel.AddToProject;
        }

        private void ButtonScanOptions_Click(object sender, RoutedEventArgs e)
        {
            ExpandScanOptionsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void GridRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsTemplatesButtonVisible));
            OnPropertyChanged(nameof(IsPreviewButtonVisible));

            if (double.IsFinite(e.NewSize.Width))
            {
                DoubleAnimationScanAnimation.From = -e.NewSize.Width;
                DoubleAnimationScanAnimation.To = e.NewSize.Width;
            }
        }

        private void ButtonAnimated_Loading(FrameworkElement sender, object args)
        {
            // prevent animations during application startup
            if (!showEntranceAnimations)
            {
                Implicit.SetShowAnimations(sender, new ImplicitAnimationSet());
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(500);
            showEntranceAnimations = true;
        }

        private void ShowTemplates()
        {
            TemplatesFlyout flyout = new TemplatesFlyout(GridRoot.ActualWidth);
            flyout.Placement = FlyoutPlacementMode.Top;
            flyout.ShowAt(GridRoot);
        }

        private void ButtonTemplates_Click(object sender, RoutedEventArgs e)
        {
            ShowTemplates();
        }

        private void MenuFlyoutItemTemplates_Click(object sender, RoutedEventArgs e)
        {
            ShowTemplates();
        }

        private static void OnAreScanOptionsVisibleChanged(DependencyObject source, DependencyPropertyChangedEventArgs args)
        {
            if (source is ScanActionsView view)
            {
                view.OnPropertyChanged(nameof(IsTemplatesButtonVisible));
                view.OnPropertyChanged(nameof(IsPreviewButtonVisible));

                view.AreScanOptionsVisibleChanged?.Invoke(view, EventArgs.Empty);
            }
        }

        private async void BorderScanAnimation_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await StoryboardScanAnimation.BeginAsync();
            }
            catch (Exception)
            {

            }
        }

        private void ButtonCancel_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            IsHoveringCancelButton = true;
        }

        private void ButtonCancel_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            IsHoveringCancelButton = false;
        }

        private void MenuFlyoutItemAddToProject_Loading(FrameworkElement sender, object args)
        {
            if (ViewModel.AddToProject)
            {
                ((MenuFlyoutItem)sender).FontWeight = FontWeights.SemiBold;
            }
            else
            {
                ((MenuFlyoutItem)sender).FontWeight = FontWeights.Normal;
            }
        }

        private void MenuFlyoutItemNewProject_Loading(FrameworkElement sender, object args)
        {
            if (!ViewModel.AddToProject)
            {
                ((MenuFlyoutItem)sender).FontWeight = FontWeights.SemiBold;
            }
            else
            {
                ((MenuFlyoutItem)sender).FontWeight = FontWeights.Normal;
            }
        }

        private void ProjectService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IProjectService.CurrentScanState):
                    this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                    {
                        ScanStateDescription = ViewModel.ProjectService.CurrentScanState.ToString();
                    });
                    break;
            }
        }
    }
}
