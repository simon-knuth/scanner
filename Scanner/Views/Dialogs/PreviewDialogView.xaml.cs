using System;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using static Enums;
using static Utilities;

namespace Scanner.Views.Dialogs
{
    public sealed partial class PreviewDialogView : ContentDialog
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public PreviewDialogView()
        {
            this.InitializeComponent();

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.PreviewFile):
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        await ImageCropperPreview.LoadImageFromFile(ViewModel.PreviewFile);
                        ImageCropperPreview_ManipulationCompleted(ImageCropperPreview, null);
                    });
                    break;
                case nameof(ViewModel.SelectedAspectRatio):
                    await SetSelectedRegionInViewModel();
                    break;
                case nameof(ViewModel.SelectedRegion):
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        if (ViewModel.SelectedRegion != null)
                        {
                            ImageCropperPreview.TrySetCroppedRegion((Windows.Foundation.Rect)ViewModel.SelectedRegion);
                        }
                    });
                    break;
            }
        }

        private async void ImageCropperPreview_ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
        {
            await SetSelectedRegionInViewModel();
        }

        private async Task SetSelectedRegionInViewModel()
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                ViewModel.SelectedRegion = new Windows.Foundation.Rect
                {
                    Width = Math.Round(ImageCropperPreview.CroppedRegion.Width),
                    Height = Math.Round(ImageCropperPreview.CroppedRegion.Height),
                    X = Math.Round(ImageCropperPreview.CroppedRegion.X),
                    Y = Math.Round(ImageCropperPreview.CroppedRegion.Y)
                };
            });
        }

        private void ContentDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            ViewModel.ClosedCommand.Execute(null);
        }

        private async void ToggleButtonToolbarAspectRatio_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Low, () =>
            {
                FlyoutBase.ShowAttachedFlyout((AppBarToggleButton)sender);
            });
        }

        private async void ToggleButtonToolbarAspectRatio_IsCheckedChanged(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                ((AppBarToggleButton)sender).IsChecked = ViewModel.IsFixedAspectRatioSelected;
            });
        }

        private void ToggleMenuFlyoutItemAspectRatio_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedAspectRatio = (AspectRatioOption)int.Parse((string)((ToggleMenuFlyoutItem)sender).Tag);
        }

        private void MenuFlyoutItemCropAspectRatioFlip_Click(object sender, RoutedEventArgs e)
        {
            // flip aspect ratio, needs to be done in code-behind because the ImageCropper
            //  doesn't properly support a binding
            ViewModel.AspectRatioFlipCommand.Execute(ImageCropperPreview.CroppedRegion);
        }

        private async void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, () =>
            {
                ((TextBox)sender).SelectAll();
            });
        }

        private void TextBox_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Accept || e.Key == VirtualKey.Enter)
            {
                ButtonApplySelection.Focus(FocusState.Programmatic);
            }
        }
    }
}
