using Microsoft.UI.Xaml.Controls;
using Scanner.Services;
using System;
using System.Threading.Tasks;
using Windows.Globalization.NumberFormatting;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Scanner.ViewModels;
using static Enums;
using static Utilities;
using Windows.Foundation;

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
                case nameof(ViewModel.SelectedAspectRatioValue):
                    await Task.Delay(500);      // ugh... 😞
                    await SetSelectedRegionInViewModel();
                    break;
                case nameof(ViewModel.MinLength):
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        ImageCropperPreview.MinCroppedPixelLength = ViewModel.MinLength.Pixels;
                    });
                    break;
                case nameof(ViewModel.SelectedX):
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Rect newRect = ImageCropperPreview.CroppedRegion;
                        newRect.X = ViewModel.SelectedX.Pixels;

                        ImageCropperPreview.TrySetCroppedRegion(newRect);
                    });
                    break;
                case nameof(ViewModel.SelectedY):
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Rect newRect = ImageCropperPreview.CroppedRegion;
                        newRect.Y = ViewModel.SelectedY.Pixels;

                        ImageCropperPreview.TrySetCroppedRegion(newRect);
                    });
                    break;
                case nameof(ViewModel.SelectedWidth):
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Rect newRect = ImageCropperPreview.CroppedRegion;
                        newRect.Width = ViewModel.SelectedWidth.Pixels;

                        ImageCropperPreview.TrySetCroppedRegion(newRect);
                    });
                    break;
                case nameof(ViewModel.SelectedHeight):
                    await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Rect newRect = ImageCropperPreview.CroppedRegion;
                        newRect.Height = ViewModel.SelectedHeight.Pixels;

                        ImageCropperPreview.TrySetCroppedRegion(newRect);
                    });
                    break;
            }
        }

        private async void ImageCropperPreview_ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
        {
            await SetSelectedRegionInViewModel();
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                ImageCropperPreview.Focus(FocusState.Programmatic);
            });
        }

        private async Task SetSelectedRegionInViewModel()
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                ViewModel.SelectedWidth = new MeasurementValue(MeasurementType.Pixels, ImageCropperPreview.CroppedRegion.Width, ViewModel.InchesPerPixel);
                ViewModel.SelectedHeight = new MeasurementValue(MeasurementType.Pixels, ImageCropperPreview.CroppedRegion.Height, ViewModel.InchesPerPixel);
                ViewModel.SelectedX = new MeasurementValue(MeasurementType.Pixels, ImageCropperPreview.CroppedRegion.X, ViewModel.InchesPerPixel);
                ViewModel.SelectedY = new MeasurementValue(MeasurementType.Pixels, ImageCropperPreview.CroppedRegion.Y, ViewModel.InchesPerPixel);
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

        private void NumberBoxWidth_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Accept || e.Key == VirtualKey.Enter)
            {
                NumberBoxSelectedHeight.Focus(FocusState.Programmatic);
            }
        }

        private void NumberBoxHeight_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Accept || e.Key == VirtualKey.Enter)
            {
                ButtonApplySelection.Focus(FocusState.Programmatic);
            }
        }

        private void TextBlockUnit_Loaded(object sender, RoutedEventArgs e)
        {
            switch ((SettingMeasurementUnit)ViewModel.SettingsService.GetSetting(AppSetting.SettingMeasurementUnits))
            {
                case SettingMeasurementUnit.Metric:
                    ((TextBlock)sender).Text = LocalizedString("TextMeasurementsUnitCentimeters");
                    break;
                case SettingMeasurementUnit.ImperialUS:
                    ((TextBlock)sender).Text = LocalizedString("TextMeasurementsUnitInches");
                    break;
            }
        }

        private void NumberBox_Loaded(object sender, RoutedEventArgs e)
        {
            NumberBox numberBox = sender as NumberBox;

            // define rounding
            IncrementNumberRounder numberRounder = new IncrementNumberRounder();
            numberRounder.Increment = 0.01;

            // define formatting
            DecimalFormatter formatter = new DecimalFormatter();
            formatter.IntegerDigits = 1;
            formatter.FractionDigits = 2;
            formatter.IsGrouped = false;
            formatter.IsDecimalPointAlwaysDisplayed = true;
            formatter.NumberRounder = numberRounder;
            numberBox.NumberFormatter = formatter;
        }

        private void NumberBoxWidth_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (ViewModel.SelectedWidth.Display != args.NewValue)
            {
                ViewModel.SelectedWidth = new MeasurementValue(MeasurementType.Display, args.NewValue, ViewModel.InchesPerPixel);
            }
        }

        private void NumberBoxHeight_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (ViewModel.SelectedHeight.Display != args.NewValue)
            {
                ViewModel.SelectedHeight = new MeasurementValue(MeasurementType.Display, args.NewValue, ViewModel.InchesPerPixel);
            }
        }
    }
}
