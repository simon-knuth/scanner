using Microsoft.AppCenter.Analytics;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

using static Utilities;


namespace Scanner
{
    public sealed partial class PreviewPage : Page
    {
        private PreviewPageIntent intent;
        private InMemoryRandomAccessStream previewStream = new InMemoryRandomAccessStream();

        public PreviewPage()
        {
            this.InitializeComponent();

            // register event listeners
            CoreApplication.GetCurrentView().TitleBar.LayoutMetricsChanged += async (titleBar, y) =>
            {
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    GridPreviewHeader.Padding = new Thickness(0, titleBar.Height, 0, 0);
                });
            };
            Window.Current.Activated += Window_Activated;
        }


        private async void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == CoreWindowActivationState.Deactivated)
            {
                // window deactivated
                await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
                {
                    TextBlockPreviewHeader.Opacity = 0.5;
                    AppBarSeparatorPreviewHeader.Opacity = 0.5;
                    TextBlockPreviewHeaderConfig.Opacity = 0.5;
                });
            }
            else
            {
                // window activated
                await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
                {
                    TextBlockPreviewHeader.Opacity = 1;
                    AppBarSeparatorPreviewHeader.Opacity = 1;
                    TextBlockPreviewHeaderConfig.Opacity = 1;
                });
            }
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            intent = (PreviewPageIntent)e.Parameter;

            try
            {
                Analytics.TrackEvent("Preview", new Dictionary<string, string> {
                        { "Source", intent.scanSource.ToString() },
                });
            }
            catch (Exception) { }
        }


        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.GoBack || e.Key == Windows.System.VirtualKey.Escape)
            {
                ButtonBack_Click(null, null);
            }
        }


        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Log.Information("Navigated to PreviewPage with {@Intent}.", intent);
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                TextBlockPreviewHeaderConfig.Text = intent.scanSourceDescription;

                ScrollViewerPreview.Margin = new Thickness(0, GridPreviewHeader.ActualHeight, 0, 0);
                ScrollViewerPreview.Padding = new Thickness(0, -GridPreviewHeader.ActualHeight, 0, 0);
                RefreshZoomUIForFactor(1);
            });

            try
            {
                var previewResult = await intent.scanner.ScanPreviewToStreamAsync(intent.scanSource, previewStream);

                if (previewResult.Succeeded)
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.SetSource(previewStream);
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        ImagePreview.Source = bitmapImage;
                        ProgressRingPreview.Visibility = Visibility.Collapsed;
                        OverlayPreview.Visibility = Visibility.Visible;
                    });
                }
                else
                {
                    throw new Exception();
                }
            }
            catch (Exception exc)
            {
                Log.Error(exc, "Preview failed.");
                await RunOnUIThreadAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    StoryboardError.Begin();
                });
            }
        }


        private void ButtonBack_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }


        private async void Page_Loading(FrameworkElement sender, object args)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                var flowDirectionSetting = Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];
                if (flowDirectionSetting == "LTR")
                {
                    GridPreviewContent.FlowDirection = FlowDirection.LeftToRight;
                    ButtonBackScaleTransform.ScaleX = 1;
                }
                else
                {
                    GridPreviewContent.FlowDirection = FlowDirection.RightToLeft;
                    ButtonBackScaleTransform.ScaleX = -1;
                }
            });

            await InitializeAutomationPropertiesAsync();
        }


        private async Task InitializeAutomationPropertiesAsync()
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                CopyToolTipToAutomationPropertiesName(ButtonBack);
            });
        }


        private async void ButtonZoomInOut_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (sender == ButtonZoomIn)
                {
                    ScrollViewer scrollViewer = ScrollViewerPreview;

                    if (scrollViewer.ZoomFactor >= 2.45) return;

                    if (scrollViewer.ZoomFactor < 1.95) TryZoomScanAsync((float)2.5, true);
                }
                else if (sender == ButtonZoomOut)
                {
                    ScrollViewer scrollViewer = ScrollViewerPreview;

                    if (scrollViewer.ZoomFactor == 1) return;

                    if (scrollViewer.ZoomFactor >= 2.45) TryZoomScanAsync(1, true);
                }
            });
        }


        private void RefreshZoomUIForFactor(float factor)
        {
            TextBlockZoomFactor.Text = String.Format(LocalizedString("TextZoomFactor"), factor * 100);
            if (factor < (float)1.05)
            {
                ButtonZoomOut.IsEnabled = false;
                ButtonZoomIn.IsEnabled = true;
                ZoomFactorAccent.Visibility = Visibility.Collapsed;
                TextBlockZoomFactor.FontWeight = FontWeights.Normal;
            }
            else if (factor < 2.45)
            {
                ButtonZoomOut.IsEnabled = true;
                ButtonZoomIn.IsEnabled = true;
                ZoomFactorAccent.Visibility = Visibility.Visible;
                TextBlockZoomFactor.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                ButtonZoomOut.IsEnabled = true;
                ButtonZoomIn.IsEnabled = false;
                ZoomFactorAccent.Visibility = Visibility.Visible;
                TextBlockZoomFactor.FontWeight = FontWeights.SemiBold;
            }
        }


        private void TryZoomScanAsync(float factor, bool animate)
        {
            if (factor < 1.02) factor = 1;

            double horizontalOffset = ScrollViewerPreview.ViewportWidth / 2 * (factor - 1);
            if (ScrollViewerPreview.ZoomFactor > 1)
            {
                double previousHorizontalOffset = ScrollViewerPreview.HorizontalOffset / (ScrollViewerPreview.ZoomFactor - 1) * (factor - 1);
                if (previousHorizontalOffset < horizontalOffset) horizontalOffset = horizontalOffset - (horizontalOffset - previousHorizontalOffset);
                else horizontalOffset = horizontalOffset + (previousHorizontalOffset - horizontalOffset);
            }

            double verticalOffset = ScrollViewerPreview.ViewportHeight / 2 * (factor - 1);
            if (ScrollViewerPreview.ZoomFactor > 1)
            {
                double previousVerticalOffset = ScrollViewerPreview.VerticalOffset / (ScrollViewerPreview.ZoomFactor - 1) * (factor - 1);
                if (previousVerticalOffset < verticalOffset) verticalOffset = verticalOffset - (verticalOffset - previousVerticalOffset);
                else verticalOffset = verticalOffset + (previousVerticalOffset - verticalOffset);
            }

            ScrollViewerPreview.ChangeView(horizontalOffset, verticalOffset, factor, !animate);
        }


        private async void ScrollViewerPreview_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                RefreshZoomUIForFactor(e.NextView.ZoomFactor);
            });
        }


        private void ScrollViewerPreview_Loading(FrameworkElement sender, object args)
        {
            IList<float> snapPoints = ((ScrollViewer)sender).ZoomSnapPoints;

            snapPoints.Add(1);
            float value = (float)1.05;
            while (value <= 2.5)
            {
                snapPoints.Add(value);
                value = (float)(value + 0.01);
            }
        }
    }
}
