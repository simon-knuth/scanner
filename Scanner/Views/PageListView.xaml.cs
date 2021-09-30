using System;
using System.Collections.Generic;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using static Utilities;

namespace Scanner.Views
{
    public sealed partial class PageListView : Page
    {
        public PageListView()
        {
            this.InitializeComponent();
        }

        private async void AppBarButtonRotate_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                FlyoutBase.ShowAttachedFlyout((AppBarButton)sender);
            });
        }

        private async void ItemsWrapGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                ItemsWrapGrid grid = (ItemsWrapGrid)sender;

                // set number of columns and apply correct sizing to items
                if (e.NewSize.Width > 350)
                {
                    grid.MaximumRowsOrColumns = 4;

                    if (grid.ActualWidth == 0) return;
                    double size = grid.ActualWidth / 4;

                    grid.ItemWidth = grid.ItemHeight = size;
                }
                else
                {
                    grid.MaximumRowsOrColumns = 2;

                    if (grid.ActualWidth == 0) return;
                    double size = grid.ActualWidth / 2;

                    grid.ItemWidth = grid.ItemHeight = size;
                }
            });
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                if (GridViewPages.Items.Count >= 1)
                {
                    int index = ViewModel.SelectedPageIndex;

                    // fix GridView initially fails to select item by binding
                    GridViewPages.SelectedIndex = index;

                    // scroll to selected item
                    GridViewItem item = (GridViewItem)GridViewPages.ContainerFromIndex(index);
                    BringIntoViewOptions options = new BringIntoViewOptions
                    {
                        AnimationDesired = false,
                    };
                    item.StartBringIntoView(options);
                }
            });
        }

        private async void GridViewPages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (GridViewPages.SelectionMode == ListViewSelectionMode.Single
                    && e.AddedItems != null & e.AddedItems.Count != 0)
                {
                    // scroll to newly selected item
                    GridViewItem item = (GridViewItem)GridViewPages.ContainerFromItem(e.AddedItems[0]);

                    BringIntoViewOptions options = new BringIntoViewOptions
                    {
                        AnimationDesired = true,
                    };
                    item.StartBringIntoView(options);
                }
                else if (GridViewPages.SelectionMode != ListViewSelectionMode.Single)
                {
                    // connect items to viewmodel
                    if (GridViewPages.SelectedRanges.Count == 0)
                    {
                        ViewModel.SelectedRanges = null;
                    }
                    else
                    {
                        ViewModel.SelectedRanges = GridViewPages.SelectedRanges;
                    }
                }
            });
        }

        private async void AppBarToggleButtonSelect_Unchecked(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                // correct item connection to viewmodel
                ViewModel.SelectedRanges = null;
            });
        }
    }
}
