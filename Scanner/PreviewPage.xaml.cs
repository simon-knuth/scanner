using Microsoft.AppCenter.Analytics;
using Serilog;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Core;
using Windows.Storage.Streams;
using Windows.UI.Core;
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
    }
}
