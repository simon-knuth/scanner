using System;
using Windows.Globalization.NumberFormatting;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using static Utilities;


namespace Scanner.Views
{
    public sealed partial class ScanOptionsView : Page
    {
        public ScanOptionsView()
        {
            this.InitializeComponent();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.ScannerSearchTipRequested += ViewModel_ScannerSearchTipRequested;
            ViewModel.ScanMergeTipRequested += ViewModel_ScanMergeTipRequested;
        }

        private async void ViewModel_ScanMergeTipRequested(object sender, EventArgs e)
        {
            await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.Normal, () =>
            {
                ReliablyOpenTeachingTip(TeachingTipScanMerge);
            });
        }

        private void ViewModel_PreviewRequested(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.NextDefaultScanAction):
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
                    {
                        switch (ViewModel.NextDefaultScanAction)
                        {
                            case ViewModels.ScanAction.AddPages:
                                MenuFlyoutItemButtonScan.FontWeight = Windows.UI.Text.FontWeights.SemiBold;
                                MenuFlyoutItemButtonScanAddToDocument.FontWeight = Windows.UI.Text.FontWeights.Normal;
                                MenuFlyoutItemButtonScanFresh.FontWeight = Windows.UI.Text.FontWeights.Normal;
                                AutomationProperties.SetName(ButtonScan, MenuFlyoutItemButtonScan.Text);
                                break;
                            case ViewModels.ScanAction.AddPagesToDocument:
                            default:
                                MenuFlyoutItemButtonScan.FontWeight = Windows.UI.Text.FontWeights.Normal;
                                MenuFlyoutItemButtonScanAddToDocument.FontWeight = Windows.UI.Text.FontWeights.SemiBold;
                                MenuFlyoutItemButtonScanFresh.FontWeight = Windows.UI.Text.FontWeights.Normal;
                                AutomationProperties.SetName(ButtonScan, MenuFlyoutItemButtonScanAddToDocument.Text);
                                break;
                            case ViewModels.ScanAction.StartFresh:
                                MenuFlyoutItemButtonScan.FontWeight = Windows.UI.Text.FontWeights.Normal;
                                MenuFlyoutItemButtonScanAddToDocument.FontWeight = Windows.UI.Text.FontWeights.Normal;
                                MenuFlyoutItemButtonScanFresh.FontWeight = Windows.UI.Text.FontWeights.SemiBold;
                                AutomationProperties.SetName(ButtonScan, MenuFlyoutItemButtonScanFresh.Text);
                                break;
                        }
                    });
                    break;
            }
        }

        private async void ComboBoxScanners_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
#if DEBUG
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                FlyoutBase.ShowAttachedFlyout(ComboBoxScanners);
            });
#endif
        }

        private async void ButtonScan_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
#if DEBUG
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
            });
#endif
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                // fix RadioButtons losing index value when navigating to multiple different pages
                try
                {
                    int index;
                    index = RadioButtonsSource.SelectedIndex;
                    RadioButtonsSource.SelectedIndex = -1;
                    RadioButtonsSource.SelectedIndex = index;

                    index = RadioButtonsColorMode.SelectedIndex;
                    RadioButtonsColorMode.SelectedIndex = -1;
                    RadioButtonsColorMode.SelectedIndex = index;

                    index = RadioButtonsAutoCropMode.SelectedIndex;
                    RadioButtonsAutoCropMode.SelectedIndex = -1;
                    RadioButtonsAutoCropMode.SelectedIndex = index;
                }
                catch { }

                // fix ProgressRing getting stuck when navigating back to cached page
                ProgressRingScanners.IsActive = false;
                ProgressRingScan.IsActive = false;
                ProgressRingScanners.IsActive = true;
                ProgressRingScan.IsActive = true;
            });
        }

        private async void ViewModel_ScannerSearchTipRequested(object sender, EventArgs e)
        {
            await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.Normal, () =>
            {
                ReliablyOpenTeachingTip(TeachingTipScannerSearchTip);
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ViewModel.ViewNavigatedToCommand.Execute(null);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            ViewModel.ViewNavigatedFromCommand.Execute(null);
        }

        private async void ButtonPreview_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
            });
        }

        private async void MenuFlyoutButtonScan_Opening(object sender, object e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                MenuFlyoutItemButtonScanMerge.IsEnabled =
                    ViewModel.CanAddToScanResultDocument
                    && (ViewModel.ScannerSource == Enums.ScannerSource.Feeder
                        || (ViewModel.ScannerSource == Enums.ScannerSource.Auto
                            && ViewModel.SelectedScanner.IsFeederAllowed));

                TeachingTipScanMerge.IsOpen = false;
            });
        }
    }
}
