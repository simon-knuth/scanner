using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using WinUI = Microsoft.UI.Xaml.Controls;
using static Utilities;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.System;
using Windows.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Xaml.Media;
using Windows.Storage;
using System.Collections.Generic;

namespace Scanner.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class EditorView : Page
    {
        public string IconStoryboardToolbarIcon = "FontIconCrop";

        public string IconStoryboardToolbarIconDone = "FontIconCropDone";

        private DataTransferManager DataTransferManager = DataTransferManager.GetForCurrentView();

        private List<StorageFile> ShareFiles;

        public EditorView()
        {
            this.InitializeComponent();

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.CropSuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Crop);
            ViewModel.RotateSuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Rotate);
            ViewModel.DrawSuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Draw);
            ViewModel.RenameSuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Rename);
            ViewModel.DeleteSuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Delete);
            ViewModel.CopySuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Copy);
            ViewModel.TargetedShareUiRequested += ViewModel_TargetedShareUiRequested;
            DataTransferManager.DataRequested += DataTransferManager_DataRequested;
        }

        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            if (ShareFiles != null && ShareFiles.Count >= 1)
            {
                args.Request.Data.SetStorageItems(ShareFiles);

                if (ShareFiles.Count == 1)
                {
                    args.Request.Data.Properties.Title = ShareFiles[0].Name;
                }
                else
                {
                    args.Request.Data.Properties.Title = LocalizedString("ShareUITitleMultipleFiles");
                }
            }
        }

        private async void ViewModel_TargetedShareUiRequested(object sender, System.Collections.Generic.List<StorageFile> e)
        {
            ShareFiles = e;
            
            Rect rectangle;
            ShareUIOptions shareUIOptions = new ShareUIOptions();

            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                GeneralTransform transform;
                transform = ButtonToolbarShare.TransformToVisual(null);
                rectangle = transform.TransformBounds(new Rect(0, 0, ButtonToolbarShare.ActualWidth, ButtonToolbarShare.ActualHeight));
                shareUIOptions.SelectionRect = rectangle;

                DataTransferManager.ShowShareUI(shareUIOptions);
            });
        }

        private async Task ApplyFlipViewOrientation(Orientation orientation)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                VirtualizingStackPanel panel = (VirtualizingStackPanel)FlipViewPages.ItemsPanelRoot;
                panel.Orientation = orientation;
            });
        }

        private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Orientation))
            {
                await ApplyFlipViewOrientation(ViewModel.Orientation);
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await ApplyFlipViewOrientation(ViewModel.Orientation);

            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                // fix ProgressRing getting stuck when navigating back to cached page
                ProgressRingLoading.IsActive = false;
                ProgressRingLoading.IsActive = true;
            });
        }

        private async void ImageEx_Loaded(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                ImageEx image = (ImageEx)sender;
                ScrollViewer scrollViewer = (ScrollViewer)image.Parent;

                image.MinWidth = scrollViewer.ViewportWidth;
                image.MaxWidth = scrollViewer.ViewportWidth;
                image.MinHeight = scrollViewer.ViewportHeight;
                image.MaxHeight = scrollViewer.ViewportHeight;

                scrollViewer.SizeChanged += async (x, y) =>
                {
                    await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
                    {
                        image.MinWidth = scrollViewer.ViewportWidth;
                        image.MaxWidth = scrollViewer.ViewportWidth;
                        image.MinHeight = scrollViewer.ViewportHeight;
                        image.MaxHeight = scrollViewer.ViewportHeight;
                    });
                };
            });
        }

        private async void AppBarButtonRotate_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                FlyoutBase.ShowAttachedFlyout((AppBarButton)sender);
            });
        }

        private async void TextBoxRename_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Accept || e.Key == VirtualKey.Enter)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
                {
                    FlyoutRename.Hide();
                    ViewModel.RenameCommand.Execute(TextBoxRename.Text);
                });
            }
        }

        private async void TextBoxRename_Loaded(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () => TextBoxRename.Focus(FocusState.Programmatic));
        }

        private async void ButtonToolbarRename_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                TextBoxRename.Text = "";
                FlyoutBase.ShowAttachedFlyout(ButtonToolbarRename);
            });
        }

        private async void StoryboardToolbarIconDoneStart_Completed(object sender, object e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () => StoryboardToolbarIconDoneFinish.Begin());
        }

        private async void PlayStoryboardToolbarIconDone(ToolbarFunction function)
        {
            switch (function)
            {
                case ToolbarFunction.Crop:
                    IconStoryboardToolbarIcon = nameof(FontIconCrop);
                    IconStoryboardToolbarIconDone = nameof(FontIconCropDone);
                    break;
                case ToolbarFunction.Rotate:
                    IconStoryboardToolbarIcon = nameof(FontIconRotate);
                    IconStoryboardToolbarIconDone = nameof(FontIconRotateDone);
                    break;
                case ToolbarFunction.Draw:
                    IconStoryboardToolbarIcon = nameof(FontIconDraw);
                    IconStoryboardToolbarIconDone = nameof(FontIconDrawDone);
                    break;
                case ToolbarFunction.Rename:
                    IconStoryboardToolbarIcon = nameof(FontIconRename);
                    IconStoryboardToolbarIconDone = nameof(FontIconRenameDone);
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

        private async void ButtonRename_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () => FlyoutRename.Hide());
        }

        private async void ButtonRenameCancel_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () => FlyoutRename.Hide());
        }

        private async void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () => FlyoutDelete.Hide());
        }

        private async void ButtonDeleteCancel_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () => FlyoutDelete.Hide());
        }

        private async void ButtonToolbarDelete_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                FlyoutBase.ShowAttachedFlyout(ButtonToolbarDelete);
            });
        }
    }

    enum ToolbarFunction
    {
        Crop,
        Rotate,
        Draw,
        Rename,
        Delete,
        Copy,
        OpenWith,
        Share
    }
}
