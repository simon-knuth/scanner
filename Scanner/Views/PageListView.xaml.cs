using System;
using System.Collections.Generic;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using static Utilities;

namespace Scanner.Views
{
    public sealed partial class PageListView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string IconStoryboardToolbarIcon = "FontIconCrop";
        public string IconStoryboardToolbarIconDone = "FontIconCropDone";


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PageListView()
        {
            this.InitializeComponent();

            ViewModel.TargetedShareUiRequested += ViewModel_TargetedShareUiRequested;
            ViewModel.RotateSuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Rotate);
            ViewModel.DeleteSuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Delete);
            ViewModel.CopySuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Copy);
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async void ViewModel_TargetedShareUiRequested(object sender, EventArgs e)
        {
            Rect rectangle;
            ShareUIOptions shareUIOptions = new ShareUIOptions();

            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                GeneralTransform transform;
                transform = AppBarButtonShare.TransformToVisual(null);
                rectangle = transform.TransformBounds(new Rect(0, 0, AppBarButtonShare.ActualWidth, AppBarButtonShare.ActualHeight));
                shareUIOptions.SelectionRect = rectangle;

                DataTransferManager.ShowShareUI(shareUIOptions);
            });
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
                    // select page in view model
                    ViewModel.SelectedPageIndex = GridViewPages.SelectedIndex;

                    // scroll to newly selected item
                    try
                    {
                        GridViewItem item = (GridViewItem)GridViewPages.ContainerFromItem(e.AddedItems[0]);

                        BringIntoViewOptions options = new BringIntoViewOptions
                        {
                            AnimationDesired = true,
                        };
                        item.StartBringIntoView(options);
                    }
                    catch (Exception)
                    {

                    }
                }
                else if (GridViewPages.SelectionMode == ListViewSelectionMode.Single)
                {
                    GridViewPages.SelectedIndex = ViewModel.SelectedPageIndex;
                }
                else if (GridViewPages.SelectionMode != ListViewSelectionMode.Single)
                {
                    // connect items to view model
                    if (GridViewPages.SelectedRanges.Count == 0)
                    {
                        ViewModel.SelectedRanges = null;
                    }
                    else
                    {
                        ViewModel.SelectedRanges = GridViewPages.SelectedRanges;
                    }

                    // select last selected item in view model
                    if (e.AddedItems != null && e.AddedItems.Count != 0)
                    {
                        int index = GridViewPages.Items.IndexOf(e.AddedItems[e.AddedItems.Count - 1]);
                        ViewModel.SelectedPageIndex = index;
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

                // select correct index after switching back to single selection mode
                if (GridViewPages.SelectionMode == ListViewSelectionMode.Single
                    && GridViewPages.SelectedIndex == -1)
                {
                    GridViewPages.SelectedIndex = ViewModel.SelectedPageIndex;
                }
            });
        }

        private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedPageIndex))
            {
                // selected page changed by view model
                await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
                {
                    if (GridViewPages.SelectionMode == ListViewSelectionMode.Single)
                    {
                        // select page in GridView
                        GridViewPages.SelectedIndex = ViewModel.SelectedPageIndex;
                    }
                });
            }
            else if (e.PropertyName == nameof(ViewModel.IsEditorEditing) && ViewModel.IsEditorEditing == true)
            {
                // editor started editing
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
                {
                    AppBarToggleButtonSelect.IsChecked = false;
                });
            }
            else if (e.PropertyName == nameof(ViewModel.ScanResult) && ViewModel.ScanResult != null)
            {
                // new ScanResult, make sure that item selection is correct
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
                {
                    GridViewPages.SelectedIndex = ViewModel.SelectedPageIndex;
                });
            }
        }

        private async void StoryboardToolbarIconDoneStart_Completed(object sender, object e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () => StoryboardToolbarIconDoneFinish.Begin());
        }

        private async void PlayStoryboardToolbarIconDone(ToolbarFunction function)
        {
            switch (function)
            {
                case ToolbarFunction.Rotate:
                    IconStoryboardToolbarIcon = nameof(FontIconRotate);
                    IconStoryboardToolbarIconDone = nameof(FontIconRotateDone);
                    break;
                case ToolbarFunction.Delete:
                    IconStoryboardToolbarIcon = nameof(FontIconDelete);
                    IconStoryboardToolbarIconDone = nameof(FontIconDeleteDone);
                    break;
                case ToolbarFunction.Copy:
                    IconStoryboardToolbarIcon = nameof(FontIconCopy);
                    IconStoryboardToolbarIconDone = nameof(FontIconCopyDone);
                    break;
                case ToolbarFunction.OpenWith:
                case ToolbarFunction.Share:
                default:
                    return;
            }

            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                StoryboardToolbarIconDoneStart.SkipToFill();
                StoryboardToolbarIconDoneStart.Stop();
                StoryboardToolbarIconDoneFinish.SkipToFill();
                StoryboardToolbarIconDoneFinish.Stop();

                int index = 0;
                foreach (var animation in StoryboardToolbarIconDoneStart.Children)
                {
                    if (index <= 2) Storyboard.SetTargetName(animation, IconStoryboardToolbarIcon);
                    else Storyboard.SetTargetName(animation, IconStoryboardToolbarIconDone);
                    index++;
                }

                index = 0;
                foreach (var animation in StoryboardToolbarIconDoneFinish.Children)
                {
                    if (index <= 2) Storyboard.SetTargetName(animation, IconStoryboardToolbarIcon);
                    else Storyboard.SetTargetName(animation, IconStoryboardToolbarIconDone);
                    index++;
                }

                StoryboardToolbarIconDoneStart.Begin();
            });
        }

        private async void AppBarButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                FlyoutBase.ShowAttachedFlyout(AppBarButtonDelete);
            });
        }

        private async void AppBarButtonDeleteDelete_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () => FlyoutDelete.Hide());
        }

        private async void AppBarButtonDeleteCancel_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () => FlyoutDelete.Hide());
        }
    }
}
