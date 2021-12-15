using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using Scanner.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using static Utilities;

namespace Scanner.Views
{
    public sealed partial class EditorView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public string IconStoryboardToolbarIcon = "FontIconCrop";
        public string IconStoryboardToolbarIconDone = "FontIconCropDone";


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public EditorView()
        {
            this.InitializeComponent();

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.CropAsCopySuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.CropAsCopy);
            ViewModel.RotateSuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Rotate);
            ViewModel.DrawAsCopySuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.DrawAsCopy);
            ViewModel.DrawSuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Draw);
            ViewModel.RenameSuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Rename);
            ViewModel.DeleteSuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Delete);
            ViewModel.CopySuccessful += (x, y) => PlayStoryboardToolbarIconDone(ToolbarFunction.Copy);
            ViewModel.TargetedShareUiRequested += ViewModel_TargetedShareUiRequested;
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
            else if (e.PropertyName == nameof(ViewModel.EditorMode))
            {
                if (ViewModel.EditorMode == EditorMode.Draw)
                {
                    await InitializeInkCanvas();
                }
            }
            else if (e.PropertyName == nameof(ViewModel.IsTouchDrawingEnabled))
            {
                await ApplyTouchDrawState();
            }
        }

        private async Task InitializeInkCanvas()
        {
            Tuple<double, double> measurements = GetImageMeasurements(ViewModel.SelectedPage.CachedImage);

            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                InkCanvasEditDraw.InkPresenter.StrokeContainer.Clear();
                InkCanvasEditDraw.Width = measurements.Item1;
                InkCanvasEditDraw.Height = measurements.Item2;
            });
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await ApplyFlipViewOrientation(ViewModel.Orientation);

            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                // fix ProgressRing getting stuck when navigating back to cached page
                ProgressRingLoading.IsActive = false;
                ProgressRingLoading.IsActive = true;

                InkCanvasEditDraw.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen;
            });

            await ApplyTouchDrawState();
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

        private async void ImageExDraw_Loaded(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                ImageEx image = (ImageEx)sender;
                Grid grid = (Grid)image.Parent;
                ScrollViewer scrollViewer = (ScrollViewer)grid.Parent;

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
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (ViewModel.ScanResult.IsImage)
                {
                    TextBoxRename.Text = ViewModel.SelectedPage.ItemDescriptor;
                }
                else
                {
                    TextBoxRename.Text = ViewModel.ScanResult.Pdf.DisplayName;
                }

                TextBoxRename.Focus(FocusState.Programmatic);
                TextBoxRename.SelectAll();
            });
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
            try
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.High, () => StoryboardToolbarIconDoneFinish.Begin());
            }
            catch (Exception) { }
        }

        private async void PlayStoryboardToolbarIconDone(ToolbarFunction function)
        {
            switch (function)
            {
                case ToolbarFunction.CropAsCopy:
                    IconStoryboardToolbarIcon = nameof(FontIconCropAsCopy);
                    IconStoryboardToolbarIconDone = nameof(FontIconCropAsCopyDone);
                    break;
                case ToolbarFunction.Rotate:
                    IconStoryboardToolbarIcon = nameof(FontIconRotate);
                    IconStoryboardToolbarIconDone = nameof(FontIconRotateDone);
                    break;
                case ToolbarFunction.DrawAsCopy:
                    IconStoryboardToolbarIcon = nameof(FontIconDrawAsCopy);
                    IconStoryboardToolbarIconDone = nameof(FontIconDrawAsCopyDone);
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
                case ToolbarFunction.Crop:
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

                try
                {
                    StoryboardToolbarIconDoneStart.Begin();
                }
                catch (Exception) { }
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

        private async void ButtonToolbarOpenWith_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                if (ViewModel.ShowOpenWithWarning)
                {
                    FlyoutBase.SetAttachedFlyout((FrameworkElement)sender, FlyoutOpenWithWarning);
                }
                else
                {
                    PrepareOpenWithFlyoutApps();
                    FlyoutBase.SetAttachedFlyout((FrameworkElement)sender, MenuFlyoutOpenWith);
                }
                FlyoutBase.ShowAttachedFlyout((AppBarButton)sender);
            });
        }

        private void PrepareOpenWithFlyoutApps()
        {
            MenuFlyoutOpenWith.Items.Clear();
            for (int i = 0; i < ViewModel.OpenWithApps.Count; i++)
            {
                OpenWithApp app = ViewModel.OpenWithApps[i];

                if (app.Logo != null)
                {
                    var icon = new ImageIcon
                    {
                        Source = app.Logo,
                        Scale = new System.Numerics.Vector3(3),
                        CenterPoint = new System.Numerics.Vector3(app.Logo.PixelWidth / 2)
                    };

                    var item = new MenuFlyoutItem
                    {
                        Text = app.AppInfo.DisplayInfo.DisplayName,
                        Icon = icon,
                        Command = ViewModel.OpenWithCommand,
                        CommandParameter = i.ToString()
                    };

                    MenuFlyoutOpenWith.Items.Add(item);
                }
                else
                {
                    var item = new MenuFlyoutItem
                    {
                        Text = app.AppInfo.DisplayInfo.DisplayName,
                        Command = ViewModel.OpenWithCommand,
                        CommandParameter = i.ToString()
                    };

                    MenuFlyoutOpenWith.Items.Add(item);
                }
            }
            MenuFlyoutOpenWith.Items.Add(MenuFlyoutItemStore);
            MenuFlyoutOpenWith.Items.Add(new MenuFlyoutSeparator());
            MenuFlyoutOpenWith.Items.Add(MenuFlyoutItemAllApps);
        }

        private async void PipsPager_SelectedIndexChanged(PipsPager sender, PipsPagerSelectedIndexChangedEventArgs args)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (sender.NumberOfPages != 0) ViewModel.SelectedPageIndex = sender.SelectedPageIndex;
            });
        }

        private async void ImageCropper_Loaded(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                ((ImageCropper)sender).LoadImageFromFile(ViewModel.SelectedPage.ScanFile);
            });
        }

        private async void ImageCropperPage_Unloaded(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                ((ImageCropper)sender).Source = null;
            });
        }

        private async void AppBarToggleButtonAspectRatio_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                FlyoutBase.ShowAttachedFlyout((AppBarToggleButton)sender);
            });
        }

        private void ToggleMenuFlyoutItemAspectRatio_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedAspectRatio = (AspectRatioOption)int.Parse((string)((ToggleMenuFlyoutItem)sender).Tag);
        }

        private async void AppBarToggleButtonAspectRatio_IsCheckedChanged(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                ((AppBarToggleButton)sender).IsChecked = ViewModel.IsFixedAspectRatioSelected;
            });
        }

        private async Task ApplyTouchDrawState()
        {
            if (ViewModel.IsTouchDrawingEnabled)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
                {
                    InkCanvasEditDraw.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Touch;
                });
            }
            else
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
                {
                    InkCanvasEditDraw.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen;
                });
            }
        }

        private async void GridFooterDraw_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                if (e.NewSize.Width < 500)
                {
                    GridFooterDrawButtons.HorizontalAlignment = HorizontalAlignment.Left;
                }
                else
                {
                    GridFooterDrawButtons.HorizontalAlignment = HorizontalAlignment.Center;
                }
            });
        }

        private void MenuFlyoutItemCropAspectRatioFlip_Click(object sender, RoutedEventArgs e)
        {
            // flip aspect ratio, needs to be done in code-behind because the ImageCropper
            //  doesn't properly support a binding
            ViewModel.AspectRatioFlipCommand.Execute(ImageCropperPage.CroppedRegion);
        }

        private async void ButtonOpenWithWarningConfirm_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                FlyoutOpenWithWarning.Hide();
                PrepareOpenWithFlyoutApps();
                FlyoutBase.SetAttachedFlyout(ButtonToolbarOpenWith, MenuFlyoutOpenWith);
                FlyoutBase.ShowAttachedFlyout(ButtonToolbarOpenWith);
            });
        }

        private async void ButtonCropSimilarPages_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
            });
        }

        private async void ButtonCropSimilarPagesCancel_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () => FlyoutCropSimilarPages.Hide());
        }

        private async void ListViewCropSimilarPages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (ListViewCropSimilarPages.SelectionMode != ListViewSelectionMode.Single)
                {
                    // connect items to view model
                    if (ListViewCropSimilarPages.SelectedRanges.Count == 0)
                    {
                        ViewModel.SelectedRangesCropSimilarPages = null;
                    }
                    else
                    {
                        ViewModel.SelectedRangesCropSimilarPages = ListViewCropSimilarPages.SelectedRanges;
                    }
                }
            });
        }

        private async void ButtonCropSimilarPagesSave_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () => FlyoutCropSimilarPages.Hide());
        }

        private async void StackPanelToolbarCrop_Loaded(object sender, RoutedEventArgs e)
        {
            // fix success animation may have gotten stuck when playing while this was hidden
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                try
                {
                    StoryboardToolbarIconDoneFinish.Begin();
                }
                catch (Exception) { }
            });
        }

        private async void StackPanelToolbarDraw_Loaded(object sender, RoutedEventArgs e)
        {
            // fix success animation may have gotten stuck when playing while this was hidden
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                try
                {
                    StoryboardToolbarIconDoneFinish.Begin();
                }
                catch (Exception) { }
            });
        }

        private async void StackPanelToolbarInitial_Loaded(object sender, RoutedEventArgs e)
        {
            // fix success animation may have gotten stuck when playing while this was hidden
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                try
                {
                    StoryboardToolbarIconDoneFinish.Begin();
                }
                catch (Exception) { }
            });
        }

        private async void ScrollViewerZoomable_Loading(FrameworkElement sender, object args)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                IList<float> snapPoints = ((ScrollViewer)sender).ZoomSnapPoints;

                snapPoints.Add(1);
                float value = (float)1.05;
                while (value <= 2.5)
                {
                    snapPoints.Add(value);
                    value = (float)(value + 0.01);
                }

                ((ScrollViewer)sender).ChangeView(0, 0, 1);

                RefreshZoomUIForFactor(1);
            });
        }

        private async void ScrollViewerFlipViewPages_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                RefreshZoomUIForFactor(e.NextView.ZoomFactor);
            });
        }

        private async void ScrollViewerEditDraw_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                RefreshZoomUIForFactor(e.NextView.ZoomFactor);
            });
        }

        private void RefreshZoomUIForFactor(float factor)
        {
            if (ViewModel.EditorMode == EditorMode.Draw)
            {
                TextBlockZoomFactorDraw.Text = String.Format(LocalizedString("TextZoomFactor"), factor * 100);
            }
            else
            {
                TextBlockZoomFactor.Text = String.Format(LocalizedString("TextZoomFactor"), factor * 100);
            }

            if (factor < (float)1.05)
            {
                if (ViewModel.EditorMode == EditorMode.Draw)
                {
                    ButtonZoomOutDraw.IsEnabled = false;
                    ButtonZoomInDraw.IsEnabled = true;
                    TextBlockZoomFactorDraw.FontWeight = FontWeights.Normal;
                }
                else
                {
                    ButtonZoomOut.IsEnabled = false;
                    ButtonZoomIn.IsEnabled = true;
                    TextBlockZoomFactor.FontWeight = FontWeights.Normal;
                }
            }
            else if (factor < 2.45)
            {
                if (ViewModel.EditorMode == EditorMode.Draw)
                {
                    ButtonZoomOutDraw.IsEnabled = true;
                    ButtonZoomInDraw.IsEnabled = true;
                    TextBlockZoomFactorDraw.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    ButtonZoomOut.IsEnabled = true;
                    ButtonZoomIn.IsEnabled = true;
                    TextBlockZoomFactor.FontWeight = FontWeights.SemiBold;
                }
            }
            else
            {
                if (ViewModel.EditorMode == EditorMode.Draw)
                {
                    ButtonZoomOutDraw.IsEnabled = true;
                    ButtonZoomInDraw.IsEnabled = false;
                    TextBlockZoomFactorDraw.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    ButtonZoomOut.IsEnabled = true;
                    ButtonZoomIn.IsEnabled = false;
                    TextBlockZoomFactor.FontWeight = FontWeights.SemiBold;
                }
            }
        }

        private async void ButtonZoomInOut_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (sender == ButtonZoomIn || sender == ButtonZoomInDraw)
                {
                    ScrollViewer scrollViewer = GetCurrentScanScrollViewer();

                    if (scrollViewer.ZoomFactor >= 2.45) return;
                    else TryZoomScanAsync((float)2.5, scrollViewer, true);
                }
                else if (sender == ButtonZoomOut || sender == ButtonZoomOutDraw)
                {
                    ScrollViewer scrollViewer = GetCurrentScanScrollViewer();

                    if (scrollViewer.ZoomFactor == 1) return;
                    else TryZoomScanAsync(1, scrollViewer, true);
                }
            });
        }

        private ScrollViewer GetCurrentScanScrollViewer()
        {
            if (ViewModel.EditorMode == EditorMode.Draw)
            {
                return ScrollViewerEditDraw;
            }
            else
            {
                if (ViewModel.SelectedPageIndex == -1) return null;
                FlipViewItem flipViewItem = (FlipViewItem)FlipViewPages.ContainerFromIndex(ViewModel.SelectedPageIndex);
                ScrollViewer scrollViewer = (ScrollViewer)flipViewItem?.ContentTemplateRoot;
                return scrollViewer;
            }
        }

        private void TryZoomScanAsync(float factor, ScrollViewer scrollViewer, bool animate)
        {
            if (factor < 1.02) factor = 1;

            try
            {
                if (null != scrollViewer)
                {
                    double horizontalOffset = scrollViewer.ViewportWidth / 2 * (factor - 1);
                    if (scrollViewer.ZoomFactor > 1)
                    {
                        double previousHorizontalOffset = scrollViewer.HorizontalOffset / (scrollViewer.ZoomFactor - 1) * (factor - 1);
                        if (previousHorizontalOffset < horizontalOffset) horizontalOffset = horizontalOffset - (horizontalOffset - previousHorizontalOffset);
                        else horizontalOffset = horizontalOffset + (previousHorizontalOffset - horizontalOffset);
                    }

                    double verticalOffset = scrollViewer.ViewportHeight / 2 * (factor - 1);
                    if (scrollViewer.ZoomFactor > 1)
                    {
                        double previousVerticalOffset = scrollViewer.VerticalOffset / (scrollViewer.ZoomFactor - 1) * (factor - 1);
                        if (previousVerticalOffset < verticalOffset) verticalOffset = verticalOffset - (verticalOffset - previousVerticalOffset);
                        else verticalOffset = verticalOffset + (previousVerticalOffset - verticalOffset);
                    }

                    scrollViewer.ChangeView(horizontalOffset, verticalOffset, factor, !animate);
                }
            }
            catch (Exception) { }
        }

        private async void FlipViewPages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                ScrollViewer scrollViewer = GetCurrentScanScrollViewer();

                RefreshZoomUIForFactor(1);

                if (scrollViewer?.ZoomFactor == 1) return;
                else TryZoomScanAsync(1, scrollViewer, true);
            });
        }

        private async void EditorDrawAboutToBeDismissed(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                if (ScrollViewerEditDraw?.ZoomFactor == 1) return;
                else TryZoomScanAsync(1, ScrollViewerEditDraw, true);
            });
        }

        private async void GridScanning_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Height != e.NewSize.Height)
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
                {
                    ScanningAnimation.Stop();
                    KeyFrameScanningAnimation.Value = $"0,{GridContent.ActualHeight - 100},0";
                    ScanningAnimation.Start();
                });
            }
        }

        private async void GridScanning_Loaded(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                KeyFrameScanningAnimation.Value = $"0,{GridContent.ActualHeight - 100},0";
            });
        }
    }

    enum ToolbarFunction
    {
        Crop,
        CropAsCopy,
        Rotate,
        Draw,
        DrawAsCopy,
        Rename,
        Delete,
        Copy,
        OpenWith,
        Share
    }
}
