using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Cryptography.Certificates;


namespace Scanner.Views
{
    [ObservableObjectAttribute]
    public sealed partial class ShellView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ShellView()
        {
            this.InitializeComponent();

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.CurrentProject):
                    this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        UpdateVisualState(GridRoot.ActualWidth);
                    });
                    break;
            }
        }

        private void UpdateVisualState(double width)
        {
            if (width < 700)
            {
                // narrow
                if (ViewModel.CurrentProject == null)
                {
                    VisualStateManager.GoToState(this, nameof(VisualStateNarrowNoProject), false);
                }
                else
                {
                    VisualStateManager.GoToState(this, nameof(VisualStateNarrow), false);
                }
            }
            else if (width < 1500)
            {
                // default
                if (ViewModel.CurrentProject == null)
                {
                    VisualStateManager.GoToState(this, nameof(VisualStateDefaultNoProject), false);
                }
                else
                {
                    VisualStateManager.GoToState(this, nameof(VisualStateDefault), false);
                }
            }
            else
            {
                // wide
                VisualStateManager.GoToState(this, nameof(VisualStateWide), false);
            }
        }

        private void SetRegionsForCustomTitleBar()
        {
            AppWindowTitleBar titlebar = ((App)Application.Current).MainWindow.AppWindow.TitleBar;

            double scaleAdjustment = this.XamlRoot.RasterizationScale;

            ColumnDefinitionTitlebarInset.Width = new GridLength((titlebar.RightInset + 24) / scaleAdjustment);

            GeneralTransform transform = StackPanelTitlebarButtonsLeft.TransformToVisual(null);
            Rect bounds = transform.TransformBounds(new Rect(0, 0,
                                                             StackPanelTitlebarButtonsLeft.ActualWidth,
                                                             StackPanelTitlebarButtonsLeft.ActualHeight));
            Windows.Graphics.RectInt32 SearchBoxRect = GetRect(bounds, scaleAdjustment);

            transform = StackPanelTitlebarButtonsRight.TransformToVisual(null);
            bounds = transform.TransformBounds(new Rect(0, 0,
                                                        StackPanelTitlebarButtonsRight.ActualWidth,
                                                        StackPanelTitlebarButtonsRight.ActualHeight));
            Windows.Graphics.RectInt32 PersonPicRect = GetRect(bounds, scaleAdjustment);

            var rectArray = new Windows.Graphics.RectInt32[] { SearchBoxRect, PersonPicRect };

            InputNonClientPointerSource nonClientInputSrc =
                InputNonClientPointerSource.GetForWindowId(((App)Application.Current).MainWindow.AppWindow.Id);
            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);
        }

        private Windows.Graphics.RectInt32 GetRect(Rect bounds, double scale)
        {
            return new Windows.Graphics.RectInt32(
                _X: (int)Math.Round(bounds.X * scale),
                _Y: (int)Math.Round(bounds.Y * scale),
                _Width: (int)Math.Round(bounds.Width * scale),
                _Height: (int)Math.Round(bounds.Height * scale)
            );
        }

        private void GridRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisualState(e.NewSize.Width);
            SetRegionsForCustomTitleBar();
        }

        private void GridRoot_Loaded(object sender, RoutedEventArgs e)
        {
            SetRegionsForCustomTitleBar();
        }

        private void ScanOptionsView_ExpandPageListRequested(object sender, EventArgs e)
        {
            ProjectView.IsExpanded = true;
            ScanOptionsView.Visibility = Visibility.Collapsed;
            ScanActionsView.AreScanOptionsVisible = true;
        }

        private void ScanActionsView_ExpandScanOptionsRequested(object sender, EventArgs e)
        {
            ScanActionsView.AreScanOptionsVisible = false;
            ScanOptionsView.Visibility = Visibility.Visible;
            ProjectView.IsExpanded = false;
        }

        private void MenuFlyoutItemHistory_Click(object sender, RoutedEventArgs e)
        {
            ShowHistory(ButtonTitlebarMore);
        }

        private void ButtonHistory_Click(object sender, RoutedEventArgs e)
        {
            ShowHistory(ButtonHistory);
        }

        private void ShowHistory(FrameworkElement target)
        {
            Flyout flyout = new Flyout();
            flyout.Content = new HistoryView
            {
                Margin = new Thickness(-16),
                MinWidth = 348,
                MinHeight = 400
            };
            flyout.ShowAt(target);
        }
    }
}
