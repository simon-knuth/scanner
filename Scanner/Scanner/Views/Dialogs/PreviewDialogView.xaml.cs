using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Extensions;
using Scanner.Helpers;
using Scanner.Models;
using Scanner.Services.Interfaces;
using Scanner.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization.NumberFormatting;

using static Scanner.Helpers.Helpers;


namespace Scanner.Views.Dialogs;

public partial class PreviewDialogView : ContentDialog
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetRequiredService<IAccessibilityService>();
    private ILogService? LogService = Ioc.Default.GetService<ILogService>();
    #endregion

    public PreviewDialogViewModel ViewModel { get; }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public PreviewDialogView(ScanOptions scanOptions)
    {
        ViewModel = new(scanOptions);
        this.InitializeComponent();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.CloseRequested += ViewModel_CloseRequested;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ViewModel.PreviewFile):
                await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.Normal, async () =>
                {
                    if (ImageCropperPreview == null)
                        return;

                    await ImageCropperPreview.LoadImageFromFile(ViewModel.PreviewFile);
                    ImageCropperPreview_ManipulationCompleted(ImageCropperPreview, null);
                });
                break;
            case nameof(ViewModel.SelectedAspectRatioValue):
                await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.High, () =>
                {
                    ImageCropperPreview?.AspectRatio = ViewModel.SelectedAspectRatioValue;
                });
                await Task.Delay(500);      // ugh... 😞
                await SetSelectedRegionInViewModelAsync();
                break;
            case nameof(ViewModel.MinLength):
                await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
                {
                    ImageCropperPreview?.MinCroppedPixelLength = ViewModel.MinLength.Pixels;
                });
                break;
            case nameof(ViewModel.SelectedX):
            case nameof(ViewModel.SelectedY):
            case nameof(ViewModel.SelectedWidth):
            case nameof(ViewModel.SelectedHeight):
                await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
                {
                    if (ViewModel.SelectedWidth == null || ViewModel.SelectedHeight == null
                        || ViewModel.SelectedX == null || ViewModel.SelectedY == null)
                        return;

                    if (ViewModel.SelectedWidth.Pixels <= 0 || ViewModel.SelectedHeight.Pixels <= 0
                        || ViewModel.SelectedX.Pixels <= 0 || ViewModel.SelectedY.Pixels <= 0)
                        return;

                    Rect newRect = ImageCropperPreview.CroppedRegion;
                    newRect.Width = ViewModel.SelectedWidth.Pixels;
                    newRect.Height = ViewModel.SelectedHeight.Pixels;
                    newRect.X = ViewModel.SelectedX.Pixels;
                    newRect.Y = ViewModel.SelectedY.Pixels;

                    if (ViewModel.IsFixedAspectRatioSelected)
                    {
                        // aspect ratio needs to be *exactly* right, so it sometimes has to be recalculated
                        ImageCropperPreview.AspectRatio = newRect.Width / newRect.Height;
                    }

                    if (e.PropertyName is nameof(ViewModel.SelectedWidth) or nameof(ViewModel.SelectedHeight))
                        ImageCropperPreview.TrySetCroppedRegion(newRect);
                });
                break;
        }
    }

    private async void ImageCropperPreview_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        await SetSelectedRegionInViewModelAsync();
        await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
        {
            ImageCropperPreview.Focus(FocusState.Programmatic);
        });
    }

    private async Task SetSelectedRegionInViewModelAsync()
    {
        await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
        {
            ViewModel.SelectedX = new MeasurementValue(MeasurementType.Pixels, ImageCropperPreview.CroppedRegion.X, ViewModel.InchesPerPixel);
            ViewModel.SelectedY = new MeasurementValue(MeasurementType.Pixels, ImageCropperPreview.CroppedRegion.Y, ViewModel.InchesPerPixel);

            if (ViewModel.SelectedWidth == null || Math.Abs(ViewModel.SelectedWidth.Pixels - ImageCropperPreview.CroppedRegion.Width) > 0.1)
                ViewModel.SelectedWidth = new MeasurementValue(MeasurementType.Pixels, ImageCropperPreview.CroppedRegion.Width, ViewModel.InchesPerPixel);

            if (ViewModel.SelectedHeight == null || Math.Abs(ViewModel.SelectedHeight.Pixels - ImageCropperPreview.CroppedRegion.Height) > 0.1)
                ViewModel.SelectedHeight = new MeasurementValue(MeasurementType.Pixels, ImageCropperPreview.CroppedRegion.Height, ViewModel.InchesPerPixel);
        });
    }

    private void ContentDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {

    }

    private void ToggleMenuFlyoutItemAspectRatio_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedAspectRatio = (AspectRatio)((ToggleMenuFlyoutItem)sender).Tag;
    }

    private void MenuFlyoutItemCropAspectRatioFlip_Click(object sender, RoutedEventArgs e)
    {
        // flip aspect ratio, needs to be done in code-behind because the ImageCropper
        //  doesn't properly support a binding
        ViewModel.AspectRatioFlipCommand.Execute(ImageCropperPreview.CroppedRegion);
    }

    private void NumberBoxWidth_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Accept || e.Key == Windows.System.VirtualKey.Enter)
        {
            NumberBoxSelectedHeight.Focus(FocusState.Programmatic);
        }
    }

    private void NumberBoxHeight_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Accept || e.Key == Windows.System.VirtualKey.Enter)
        {
            ButtonApplySelection.Focus(FocusState.Programmatic);
        }
    }

    private void TextBlockUnit_Loaded(object sender, RoutedEventArgs e)
    {
        switch (ViewModel.SettingsService.SettingMeasurementUnits)
        {
            case SettingMeasurementUnits.Metric:
                ((TextBlock)sender).Text = GetLocalized(Scanner.Resources.Strings.ResourcesExtension.KeyEnum.MeasurementsUnitCentimeters);
                break;
            case SettingMeasurementUnits.ImperialUS:
                ((TextBlock)sender).Text = GetLocalized(Scanner.Resources.Strings.ResourcesExtension.KeyEnum.MeasurementsUnitInches);
                break;
        }
    }

    private void NumberBox_Loaded(object sender, RoutedEventArgs e)
    {
        NumberBox numberBox = sender as NumberBox;

        // define rounding
        IncrementNumberRounder numberRounder = new IncrementNumberRounder
        {
            Increment = 0.01
        };

        // define formatting
        DecimalFormatter formatter = new DecimalFormatter
        {
            IntegerDigits = 1,
            FractionDigits = 2,
            IsGrouped = false,
            IsDecimalPointAlwaysDisplayed = true,
            NumberRounder = numberRounder
        };
        numberBox.NumberFormatter = formatter;
    }

    private void NumberBoxWidth_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (ViewModel.SelectedWidth?.Display != args.NewValue)
        {
            ViewModel.SelectedWidth = new MeasurementValue(MeasurementType.Display, args.NewValue, ViewModel.InchesPerPixel);
        }
    }

    private void NumberBoxHeight_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (ViewModel.SelectedHeight?.Display != args.NewValue)
        {
            ViewModel.SelectedHeight = new MeasurementValue(MeasurementType.Display, args.NewValue, ViewModel.InchesPerPixel);
        }
    }

    private void ViewModel_CloseRequested(object sender, EventArgs e)
    {
        this.Hide();
    }

    private void ButtonBack_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsCustomRegionSelected = false;
    }

    private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.ViewLoaded(DispatcherQueue);
    }
}
