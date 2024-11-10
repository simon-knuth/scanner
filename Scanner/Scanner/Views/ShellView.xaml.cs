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
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
            SetRegionsForCustomTitleBar();
        }

        private void GridRoot_Loaded(object sender, RoutedEventArgs e)
        {
            SetRegionsForCustomTitleBar();
        }

        private void VisualStateGroup_CurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            //ApplyVisualState(e.NewState);
        }

        //private void ApplyVisualState(VisualState state)
        //{
        //    if (state == VisualStateNarrow)
        //    {
        //        BorderScanOptionsRight.Child = null;
        //        BorderScanActionsRight.Child = null;

        //        BorderScanOptionsLeft.Child = ScanOptionsView;
        //        BorderScanActionsLeft.Child = ScanActionsView;
        //    }
        //    else if (state == VisualStateDefault)
        //    {
        //        BorderScanOptionsRight.Child = null;
        //        BorderScanActionsRight.Child = null;

        //        BorderScanOptionsLeft.Child = ScanOptionsView;
        //        BorderScanActionsLeft.Child = ScanActionsView;
        //    }
        //    else if (state == VisualStateWide)
        //    {
        //        BorderScanOptionsLeft.Child = null;
        //        BorderScanActionsLeft.Child = null;

        //        BorderScanOptionsRight.Child = ScanOptionsView;
        //        BorderScanActionsRight.Child = ScanActionsView;
        //    }
        //}

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

        private void GridRoot_Loading(FrameworkElement sender, object args)
        {
            //ApplyVisualState(VisualStateGroup.CurrentState);
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
