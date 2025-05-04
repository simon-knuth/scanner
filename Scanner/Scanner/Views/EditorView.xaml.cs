using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using static Scanner.Helpers.Helpers;


namespace Scanner.Views
{
    [ObservableObjectAttribute]
    public sealed partial class EditorView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Constants
        private const float minZoomFactor = 1.0f;
        private const float maxZoomFactor = 2.5f;
        #endregion

        [ObservableProperty]
        private double pageWidth;

        [ObservableProperty]
        private double pageHeight;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FriendlyPageZoomFactor))]
        [NotifyPropertyChangedFor(nameof(IsToolbarBackgroundVisible))]
        [NotifyPropertyChangedFor(nameof(CanZoomIn))]
        [NotifyPropertyChangedFor(nameof(CanZoomOut))]
        private float pageZoomFactor = 1.0f;

        public string FriendlyPageZoomFactor => string.Format(GetLocalized("TextZoomFactor"), PageZoomFactor * 100);

        public bool IsToolbarBackgroundVisible => PageZoomFactor > 1.0f;

        [ObservableProperty]
        private bool isHoveringZoomControls;

        public bool CanZoomIn => PageZoomFactor < maxZoomFactor - 0.009f;
        public bool CanZoomOut => PageZoomFactor > minZoomFactor + 0.009f;

        private VirtualizingStackPanel? flipViewPanel;

        private ScrollViewer? _selectedItemScrollViewer;
        private ScrollViewer? selectedItemScrollViewer
        {
            get => _selectedItemScrollViewer;
            set
            {
                if (_selectedItemScrollViewer != null)
                {
                    _selectedItemScrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
                }

                _selectedItemScrollViewer = value;

                if (value != null)
                {
                    value.ViewChanged += ScrollViewer_ViewChanged;
                }
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public EditorView()
        {
            this.InitializeComponent();

            ViewModel.SettingsService.PropertyChanged += SettingsService_PropertyChanged;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ButtonRotate_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private void SettingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ISettingsService.SettingEditorOrientation):
                    _ = ApplyFlipViewOrientationAsync(SettingEditorOrientationToOrientation(ViewModel.SettingsService.SettingEditorOrientation));
                    break;
            }
        }

        private async Task ApplyFlipViewOrientationAsync(Orientation orientation)
        {
            await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
            {
                if (flipViewPanel != null)
                {
                    flipViewPanel.Orientation = orientation;
                }

                // fix scrolling in vertical mode
                if (orientation == Orientation.Vertical)
                {
                    ((ScrollViewer)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(FlipViewPages, 0), 0)).HorizontalScrollMode = ScrollMode.Disabled;
                }
                else
                {
                    ((ScrollViewer)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(FlipViewPages, 0), 0)).HorizontalScrollMode = ScrollMode.Enabled;
                }
            });
        }

        private Orientation SettingEditorOrientationToOrientation(SettingEditorOrientation setting)
        {
            return setting switch
            {
                SettingEditorOrientation.Horizontal => Orientation.Horizontal,
                SettingEditorOrientation.Vertical => Orientation.Vertical,
                _ => Orientation.Horizontal,
            };
        }

        private void VirtualizingStackPanel_Loading(FrameworkElement sender, object args)
        {
            flipViewPanel = sender as VirtualizingStackPanel;
            _ = ApplyFlipViewOrientationAsync(SettingEditorOrientationToOrientation(ViewModel.SettingsService.SettingEditorOrientation));

            if (flipViewPanel != null)
            {
                foreach (FlipViewItem item in flipViewPanel.Children)
                {
                    ScrollViewer? scrollViewer = item.FindDescendant<ScrollViewer>();
                    if (item.IsSelected)
                    {
                        selectedItemScrollViewer = scrollViewer;
                        return;
                    }
                }
            }
        }

        private void ScrollViewerPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScrollViewer scrollViewer = (ScrollViewer)sender;

            PageWidth = scrollViewer.ViewportWidth;
            PageHeight = scrollViewer.ViewportHeight;
        }

        private void FlipViewPages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InitializeZoomProperties();
        }
        
        private void InitializeZoomProperties()
        {
            // reset zoom factor for unselected items
            if (flipViewPanel == null) return;

            selectedItemScrollViewer = null;
            PageZoomFactor = 1.0f;
            foreach (FlipViewItem item in flipViewPanel.Children)
            {
                ScrollViewer? scrollViewer = item.FindDescendant<ScrollViewer>();
                if (item.IsSelected)
                {
                    selectedItemScrollViewer = scrollViewer;
                }
                else
                {
                    scrollViewer?.ChangeView(null, null, 1.0f, true);
                }
            }
        }

        private void ScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (selectedItemScrollViewer == null || sender is not ScrollViewer scrollViewer) return;
            PageZoomFactor = selectedItemScrollViewer.ZoomFactor;
        }

        private void ButtonPageZoomFactor_Click(object sender, RoutedEventArgs e)
        {
            selectedItemScrollViewer?.ChangeView(null, null, 1.0f);
        }

        private void FlipViewPages_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeZoomProperties();
        }

        private void GridToolbarZoom_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            IsHoveringZoomControls = true;
        }

        private void GridToolbarZoom_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            IsHoveringZoomControls = false;
        }

        private void ButtonPageZoomFactorIncrease_Click(object sender, RoutedEventArgs e)
        {
            TryZoomScanAsync(0.5f, true);
        }

        private void ButtonPageZoomFactorDecrease_Click(object sender, RoutedEventArgs e)
        {
            TryZoomScanAsync(-0.5f, true);
        }

        private void FlipViewItem_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            InitializeZoomProperties();
        }

        private void FlipViewItemPage_Loaded(object sender, RoutedEventArgs e)
        {
            ((FlipViewItem)sender).RegisterPropertyChangedCallback(FlipViewItem.IsSelectedProperty, (s, e) =>
            {
                InitializeZoomProperties();
            });
        }

        private void TryZoomScanAsync(float change, bool animate)
        {
            if (selectedItemScrollViewer == null) return;

            float newFactor = selectedItemScrollViewer.ZoomFactor + change;
            if (newFactor > maxZoomFactor) newFactor = maxZoomFactor;
            if (newFactor < minZoomFactor) newFactor = minZoomFactor;

            try
            {
                // Calculate the center of the viewport in content coordinates before zooming
                double horizontalCenter = selectedItemScrollViewer.HorizontalOffset + (selectedItemScrollViewer.ViewportWidth / 2);
                double verticalCenter = selectedItemScrollViewer.VerticalOffset + (selectedItemScrollViewer.ViewportHeight / 2);

                // Preserve the center point correctly after zooming
                double scaleRatio = newFactor / selectedItemScrollViewer.ZoomFactor;

                double newHorizontalOffset = Math.Max((horizontalCenter * scaleRatio) - (selectedItemScrollViewer.ViewportWidth / 2), 0);
                double newVerticalOffset = Math.Max((verticalCenter * scaleRatio) - (selectedItemScrollViewer.ViewportHeight / 2), 0);

                selectedItemScrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, newFactor, !animate);
            }
            catch (Exception) { }
        }
    }
}
