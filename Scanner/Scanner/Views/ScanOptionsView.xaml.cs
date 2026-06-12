using CommunityToolkit.Labs.WinUI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Extensions;
using Scanner.Helpers;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;


namespace Scanner.Views;

[ObservableObjectAttribute]
public sealed partial class ScanOptionsView : Page
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
    #region Events
    public event EventHandler? ExpandPageListRequested;
    #endregion

    #region Constants
    private const int invalidScannerItems = 3;
    #endregion

    #region Dependency Properties
    public static readonly DependencyProperty CanExpandPageListProperty =
        DependencyProperty.Register(nameof(CanExpandPageList), typeof(bool), typeof(ScanOptionsView), null);

    public static readonly DependencyProperty ScanOptionsProperty =
        DependencyProperty.Register(nameof(ScanOptions), typeof(ScanOptions), typeof(ScanOptionsView), null);
    #endregion

    public bool CanExpandPageList
    {
        get => (bool)GetValue(CanExpandPageListProperty);
        set => SetValue(CanExpandPageListProperty, value);
    }

    public ScanOptions ScanOptions
    {
        get => ViewModel.ScanOptions;
        set
        {
            SetValue(ScanOptionsProperty, value);
            ViewModel.ScanOptions = value;
        }
    }

    [ObservableProperty]
    private int scannerCount = 0;

    #region Source mode
    public bool IsSourceModeAutomatic
    {
        get => ViewModel.ScanOptions.SourceMode == ScannerSource.Auto;
        set
        {
            if (value)
            {
                ViewModel.ScanOptions.SourceMode = ScannerSource.Auto;
            }
        }
    }

    public bool IsSourceModeFlatbed
    {
        get => ViewModel.ScanOptions.SourceMode == ScannerSource.Flatbed;
        set
        {
            if (value)
            {
                ViewModel.ScanOptions.SourceMode = ScannerSource.Flatbed;
            }
        }
    }

    public bool IsSourceModeFeeder
    {
        get => ViewModel.ScanOptions.SourceMode == ScannerSource.Feeder;
        set
        {
            if (value)
            {
                ViewModel.ScanOptions.SourceMode = ScannerSource.Feeder;
            }
        }
    }
    #endregion

    public int TargetFormat
    {
        // work around additional ComboBoxItems
        get
        {
            if (ViewModel.ScanOptions.TargetFormat == Models.TargetFormat.None || ViewModel.SelectedScanner == null)
            {
                return -1;
            }
            if ((int)ViewModel.ScanOptions.TargetFormat > 1)
            {
                return (int)ViewModel.ScanOptions.TargetFormat + 1;
            }
            else
            {
                return (int)Models.TargetFormat.PDF;
            }
        }
        set
        {
            if (value == -1)
            {
                ViewModel.ScanOptions.TargetFormat = Models.TargetFormat.None;
            }
            if (value > 1)
            {
                ViewModel.ScanOptions.TargetFormat = (TargetFormat)value - 1;
            }
            else
            {
                ViewModel.ScanOptions.TargetFormat = Models.TargetFormat.PDF;
            }
        }
    }

    #region Color mode
    public bool IsColorModeColor
    {
        get => ViewModel.ScanOptions.ColorMode == ScannerColorMode.Color;
        set
        {
            if (value)
            {
                ViewModel.ScanOptions.ColorMode = ScannerColorMode.Color;
            }
        }
    }

    public bool IsColorModeColorSupported
    {
        get
        {
            if (ViewModel.SelectedScanner == null) return false;

            switch (ViewModel.ScanOptions.SourceMode)
            {
                case ScannerSource.Flatbed:
                    return ViewModel.SelectedScanner.IsFlatbedColorAllowed;
                case ScannerSource.Feeder:
                    return ViewModel.SelectedScanner.IsFeederColorAllowed;
                case ScannerSource.Auto:
                case ScannerSource.None:
                    default:
                        return false;
            }
        }
    }

    public bool IsColorModeGrayscale
    {
        get => ViewModel.ScanOptions.ColorMode == ScannerColorMode.Grayscale;
        set
        {
            if (value)
            {
                ViewModel.ScanOptions.ColorMode = ScannerColorMode.Grayscale;
            }
        }
    }

    public bool IsColorModeGrayscaleSupported
    {
        get
        {
            if (ViewModel.SelectedScanner == null) return false;

            if (IsColorModeColorSupported)
            {
                // can apply filter
                return true;
            }
            else
            {
                switch (ViewModel.ScanOptions.SourceMode)
                {
                    case ScannerSource.Flatbed:
                        return ViewModel.SelectedScanner.IsFlatbedGrayscaleAllowed;
                    case ScannerSource.Feeder:
                        return ViewModel.SelectedScanner.IsFeederGrayscaleAllowed;
                    case ScannerSource.Auto:
                    case ScannerSource.None:
                    default:
                        return false;
                }
            }
        }
    }

    public bool IsColorModeMonochrome
    {
        get => ViewModel.ScanOptions.ColorMode == ScannerColorMode.Monochrome;
        set
        {
            if (value)
            {
                ViewModel.ScanOptions.ColorMode = ScannerColorMode.Monochrome;
            }
        }
    }

    public bool IsColorModeMonochromeSupported
    {
        get
        {
            if (ViewModel.SelectedScanner == null) return false;

            if (IsColorModeColorSupported || IsColorModeGrayscaleSupported)
            {
                // can apply filter
                return true;
            }
            else
            {
                switch (ViewModel.ScanOptions.SourceMode)
                {
                    case ScannerSource.Flatbed:
                        return ViewModel.SelectedScanner.IsFlatbedMonochromeAllowed;
                    case ScannerSource.Feeder:
                        return ViewModel.SelectedScanner.IsFeederMonochromeAllowed;
                    case ScannerSource.Auto:
                    case ScannerSource.None:
                    default:
                        return false;
                }
            }
        }
    }
    #endregion

    #region Resolutions
    public List<ScanResolution> Resolutions
    {
        get
        {
            if (ViewModel.SelectedScanner == null) return new();
            switch (ViewModel.ScanOptions.SourceMode)
            {
                case ScannerSource.Flatbed:
                    return ViewModel.SelectedScanner.FlatbedResolutions;
                case ScannerSource.Feeder:
                    return ViewModel.SelectedScanner.FeederResolutions;
                case ScannerSource.Auto:
                case ScannerSource.None:
                default:
                    return new();
            }
        }
    }

    public ScanResolution SelectedResolution        // required to make sure Resolutions list is ready before the ComboBox applies this value
    {
        get => ViewModel.ScanOptions.Resolution;
        set
        {
            ViewModel.ScanOptions.Resolution = value;
        }
    }
    #endregion

    #region Scan area
    public bool IsScanAreaSelectionVisible => ViewModel.SelectedScanner != null && ViewModel.ScanOptions.SourceMode is ScannerSource.Flatbed or ScannerSource.Feeder;
    public bool IsScanAreaAlignmentVisible => ViewModel.SelectedScanner != null && ViewModel.ScanOptions.SourceMode is ScannerSource.Flatbed && IsPaperSizeAreaSelected;

    public Size MaxScanArea
    {
        get
        {
            if (ViewModel.SelectedScanner == null)
                return new Size(0, 0);

            switch (ViewModel.ScanOptions.SourceMode)
            {
                case ScannerSource.Flatbed:
                    return ViewModel.SelectedScanner.FlatbedMaxScanArea;
                case ScannerSource.Feeder:
                    return ViewModel.SelectedScanner.FeederMaxScanArea;
                case ScannerSource.Auto:
                case ScannerSource.None:
                default:
                    return new Size(0, 0);
            }
        }
    }

    public bool IsPaperSizeAreaSelected => ViewModel.ScanOptions.ScanArea is PaperSizeArea;

    public PaperSize SelectedPaperSize => ScanOptions.ScanArea is PaperSizeArea paperSizeArea ? paperSizeArea.PaperSize : PaperSize.DinA4;

    public int SelectedOrientationInt
    {
        get => ScanOptions.ScanArea is PaperSizeArea paperSizeArea ? (int)paperSizeArea.Orientation : -1;
        set
        {
            if (ScanOptions.ScanArea is PaperSizeArea paperSizeArea)
                paperSizeArea.Orientation = (ScanOrientation)value;
        }
    }

    public ScanOrientation SelectedOrientation
    {
        get => ScanOptions.ScanArea is PaperSizeArea paperSizeArea ? paperSizeArea.Orientation : ScanOrientation.Portrait;
        set
        {
            if (ScanOptions.ScanArea is PaperSizeArea paperSizeArea)
                paperSizeArea.Orientation = value;
        }
    }
    
    public bool IsTopLeftScanCornerSelected
    {
        get => ScanOptions.ScanArea is PaperSizeArea paperSizeArea ? paperSizeArea.Corner == ScanCorner.TopLeft : false;
        set
        {
            if (value && ScanOptions.ScanArea is PaperSizeArea paperSizeArea)
                paperSizeArea.Corner = ScanCorner.TopLeft;
        }
    }

    public ScanCorner SelectedCorner
    {
        get => ScanOptions.ScanArea is PaperSizeArea paperSizeArea ? paperSizeArea.Corner : ScanCorner.TopLeft;
        set
        {
            if (ScanOptions.ScanArea is PaperSizeArea paperSizeArea)
                paperSizeArea.Corner = value;
        }
    }

    public bool IsTopRightScanCornerSelected
    {
        get => ScanOptions.ScanArea is PaperSizeArea paperSizeArea ? paperSizeArea.Corner == ScanCorner.TopRight : false;
        set
        {
            if (value && ScanOptions.ScanArea is PaperSizeArea paperSizeArea)
                paperSizeArea.Corner = ScanCorner.TopRight;
        }
    }

    public bool IsBottomRightScanCornerSelected
    {
        get => ScanOptions.ScanArea is PaperSizeArea paperSizeArea ? paperSizeArea.Corner == ScanCorner.BottomRight : false;
        set
        {
            if (value && ScanOptions.ScanArea is PaperSizeArea paperSizeArea)
                paperSizeArea.Corner = ScanCorner.BottomRight;
        }
    }

    public bool IsBottomLeftScanCornerSelected
    {
        get => ScanOptions.ScanArea is PaperSizeArea paperSizeArea ? paperSizeArea.Corner == ScanCorner.BottomLeft : false;
        set
        {
            if (value && ScanOptions.ScanArea is PaperSizeArea paperSizeArea)
                paperSizeArea.Corner = ScanCorner.BottomLeft;
        }
    }

    public string PaperSizeDinA3Dimensions => PaperSize.DinA3.ToDimensionsString();
    public string PaperSizeDinA4Dimensions => PaperSize.DinA4.ToDimensionsString();
    public string PaperSizeDinA5Dimensions => PaperSize.DinA5.ToDimensionsString();
    public string PaperSizeAnsiADimensions => PaperSize.AnsiA.ToDimensionsString();
    public string PaperSizeAnsiBDimensions => PaperSize.AnsiB.ToDimensionsString();
    public string PaperSizeAnsiCDimensions => PaperSize.AnsiC.ToDimensionsString();
    public string PaperSizeLegalDimensions => PaperSize.Legal.ToDimensionsString();
    #endregion

    #region Auto crop
    public bool IsAutoCropDisabled
    {
        get => ViewModel.ScanOptions.ScanArea is not AutoCropArea autoCropRegion || autoCropRegion.AutoCropMode == ScannerAutoCropMode.Disabled;
        set
        {
            if (value)
            {
                ViewModel.ScanOptions.ScanArea = new AutoCropArea
                {
                    AutoCropMode = ScannerAutoCropMode.Disabled
                };
            }
        }
    }

    public bool IsAutoCropSingle
    {
        get => ViewModel.ScanOptions.ScanArea is AutoCropArea autoCropRegion && autoCropRegion.AutoCropMode == ScannerAutoCropMode.SingleRegion;
        set
        {
            if (value)
            {
                ViewModel.ScanOptions.ScanArea = new AutoCropArea
                {
                    AutoCropMode = ScannerAutoCropMode.SingleRegion
                };
            }
        }
    }

    public bool IsAutoCropSingleSupported
    {
        get
        {
            if (ViewModel.SelectedScanner == null) return false;

            switch (ViewModel.ScanOptions.SourceMode)
            {
                case ScannerSource.Flatbed:
                    return ViewModel.SelectedScanner.IsFlatbedAutoCropSingleRegionAllowed;
                case ScannerSource.Feeder:
                    return ViewModel.SelectedScanner.IsFeederAutoCropSingleRegionAllowed;
                case ScannerSource.Auto:
                case ScannerSource.None:
                default:
                    return false;
            }
        }
    }

    public bool IsAutoCropMulti
    {
        get => ViewModel.ScanOptions.ScanArea is AutoCropArea autoCropRegion && autoCropRegion.AutoCropMode == ScannerAutoCropMode.MultipleRegions;
        set
        {
            if (value)
            {
                ViewModel.ScanOptions.ScanArea = new AutoCropArea
                {
                    AutoCropMode = ScannerAutoCropMode.MultipleRegions
                };
            }
        }
    }

    public bool IsAutoCropMultiSupported
    {
        get
        {
            if (ViewModel.SelectedScanner == null) return false;

            switch (ViewModel.ScanOptions.SourceMode)
            {
                case ScannerSource.Flatbed:
                    return ViewModel.SelectedScanner.IsFlatbedAutoCropMultiRegionAllowed;
                case ScannerSource.Feeder:
                    return ViewModel.SelectedScanner.IsFeederAutoCropMultiRegionAllowed;
                case ScannerSource.Auto:
                case ScannerSource.None:
                default:
                    return false;
            }
        }
    }
    #endregion

    #region Brightness & contrast
    public bool CanResetBrightness => ViewModel.ScanOptions.Brightness != 0;
    public bool CanResetContrast => ViewModel.ScanOptions.Contrast != 0;
    #endregion

    [ObservableProperty]
    private bool isScanAreaAlignmentFlyoutOpen;

    public bool CanScroll => ScrollViewerContent.ScrollableHeight > 0
        && ScrollViewerContent.VerticalOffset + 8 < ScrollViewerContent.ScrollableHeight;

    private bool IsColorModeResolutionBrightnessContrastVisible => ViewModel.ScanOptions.SourceMode is ScannerSource.Flatbed or ScannerSource.Feeder;
    private bool IsAutoCropVisible => ViewModel.SelectedScanner != null
        && ((ViewModel.ScanOptions?.SourceMode == ScannerSource.Flatbed && ViewModel.SelectedScanner.IsFlatbedAutoCropSupported)
        || (ViewModel.ScanOptions?.SourceMode == ScannerSource.Feeder && ViewModel.SelectedScanner.IsFeederAutoCropSupported));

    private bool IsDuplexVisible => ViewModel.SelectedScanner != null
        && ViewModel.ScanOptions?.SourceMode == ScannerSource.Feeder
        && ViewModel.SelectedScanner.IsFeederDuplexSupported;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ScanOptionsView()
    {
        this.InitializeComponent();
        Ioc.Default.GetService<ILogService>()?.Log.Information("View loaded");

        ViewModel.PropertyChanging += ViewModel_PropertyChanging;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.Scanners.CollectionChanged += Scanners_CollectionChanged;
        ViewModel.SettingsService.PropertyChanged += SettingsService_PropertyChanged;

        if (ViewModel.ScanOptions != null)
        {
            ViewModel.ScanOptions.PropertyChanged += ScanOptions_PropertyChanged;
            ViewModel.ScanOptions.PropertyChanging += ScanOptions_PropertyChanging;
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void ViewModel_PropertyChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ViewModel.ScanOptions):
                if (ViewModel.ScanOptions != null)
                {
                    ViewModel.ScanOptions.PropertyChanged -= ScanOptions_PropertyChanged;
                    ViewModel.ScanOptions.PropertyChanging -= ScanOptions_PropertyChanging;
                }
                break;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ViewModel.SelectedScanner):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
                {
                    if (ComboBoxScanners.SelectedItem == null || ((FrameworkElement)ComboBoxScanners.SelectedItem).Tag != ViewModel.SelectedScanner)
                    {
                        // find corresponding ComboBoxItem
                        int index = -1;
                        for (int i = 0; i < ComboBoxScanners.Items.Count - 1; i++)
                        {
                            if (((FrameworkElement)ComboBoxScanners.Items[i]).Tag == ViewModel.SelectedScanner)
                            {
                                index = i;
                                break;
                            }
                        }

                        // select correct scanner
                        if (index != -1)
                        {
                            ComboBoxScanners.SelectedIndex = index;
                        }
                        else
                        {
                            ComboBoxScanners.SelectedIndex = ComboBoxScanners.Items.Count - invalidScannerItems;
                        }

                        FlyoutScanAreaAlignment?.Hide();
                    }
                });
                break;
            case nameof(ViewModel.ScanOptions):
                if (ViewModel.ScanOptions != null)
                {
                    ViewModel.ScanOptions.PropertyChanged += ScanOptions_PropertyChanged;
                    
                    if (ViewModel.ScanOptions.ScanArea is PaperSizeArea paperSizeArea)
                        paperSizeArea.PropertyChanged += PaperSizeArea_PropertyChanged;
                }

                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(IsSourceModeAutomatic));
                    OnPropertyChanged(nameof(IsSourceModeFlatbed));
                    OnPropertyChanged(nameof(IsSourceModeFeeder));
                    OnPropertyChanged(nameof(TargetFormat));
                    OnPropertyChanged(nameof(IsColorModeResolutionBrightnessContrastVisible));
                    OnPropertyChanged(nameof(IsColorModeColor));
                    OnPropertyChanged(nameof(IsColorModeColorSupported));
                    OnPropertyChanged(nameof(IsColorModeGrayscale));
                    OnPropertyChanged(nameof(IsColorModeGrayscaleSupported));
                    OnPropertyChanged(nameof(IsColorModeMonochrome));
                    OnPropertyChanged(nameof(IsColorModeMonochromeSupported));
                    OnPropertyChanged(nameof(TargetFormat));
                    OnPropertyChanged(nameof(Resolutions));
                    OnPropertyChanged(nameof(SelectedResolution));
                    OnPropertyChanged(nameof(CanResetBrightness));
                    OnPropertyChanged(nameof(CanResetContrast));
                    OnPropertyChanged(nameof(IsAutoCropVisible));
                    OnPropertyChanged(nameof(IsAutoCropDisabled));
                    OnPropertyChanged(nameof(IsAutoCropSingle));
                    OnPropertyChanged(nameof(IsAutoCropSingleSupported));
                    OnPropertyChanged(nameof(IsAutoCropMulti));
                    OnPropertyChanged(nameof(IsAutoCropMultiSupported));
                    OnPropertyChanged(nameof(IsDuplexVisible));
                    OnPropertyChanged(nameof(MaxScanArea));
                    OnPropertyChanged(nameof(IsPaperSizeAreaSelected));
                    OnPropertyChanged(nameof(IsScanAreaSelectionVisible));
                    OnPropertyChanged(nameof(IsScanAreaAlignmentVisible));
                    OnPropertyChanged(nameof(IsTopLeftScanCornerSelected));
                    OnPropertyChanged(nameof(IsTopRightScanCornerSelected));
                    OnPropertyChanged(nameof(IsBottomLeftScanCornerSelected));
                    OnPropertyChanged(nameof(IsBottomRightScanCornerSelected));
                    OnPropertyChanged(nameof(SelectedOrientation));
                    OnPropertyChanged(nameof(SelectedOrientationInt));

                    OnPropertyChanged(nameof(ScanOptions));

                    ApplyScanAreaToComboBox(ScanOptions.ScanArea);
                });
                break;
        }
    }

    private void SettingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ISettingsService.SettingMeasurementUnits))
            return;

        OnPropertyChanged(nameof(PaperSizeDinA3Dimensions));
        OnPropertyChanged(nameof(PaperSizeDinA4Dimensions));
        OnPropertyChanged(nameof(PaperSizeDinA5Dimensions));
        OnPropertyChanged(nameof(PaperSizeAnsiADimensions));
        OnPropertyChanged(nameof(PaperSizeAnsiBDimensions));
        OnPropertyChanged(nameof(PaperSizeAnsiCDimensions));
        OnPropertyChanged(nameof(PaperSizeLegalDimensions));
    }

    private void ScanOptions_PropertyChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(ScanOptions.ScanArea))
        {
            if (ScanOptions.ScanArea is PaperSizeArea paperSizeArea)
                paperSizeArea.PropertyChanged -= PaperSizeArea_PropertyChanged;
        }
    }

    private void ScanOptions_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // force update for dependant properties
        switch (e.PropertyName)
        {
            case nameof(ScanOptions.SourceMode):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(IsSourceModeAutomatic));
                    OnPropertyChanged(nameof(IsSourceModeFlatbed));
                    OnPropertyChanged(nameof(IsSourceModeFeeder));
                    OnPropertyChanged(nameof(IsColorModeResolutionBrightnessContrastVisible));
                    OnPropertyChanged(nameof(IsColorModeColorSupported));
                    OnPropertyChanged(nameof(IsColorModeGrayscaleSupported));
                    OnPropertyChanged(nameof(IsColorModeMonochromeSupported));
                    OnPropertyChanged(nameof(Resolutions));
                    OnPropertyChanged(nameof(SelectedResolution));
                    OnPropertyChanged(nameof(IsAutoCropVisible));
                    OnPropertyChanged(nameof(IsAutoCropSingleSupported));
                    OnPropertyChanged(nameof(IsAutoCropMultiSupported));
                    OnPropertyChanged(nameof(IsAutoCropDisabled));
                    OnPropertyChanged(nameof(IsAutoCropSingle));
                    OnPropertyChanged(nameof(IsAutoCropMulti));
                    OnPropertyChanged(nameof(IsDuplexVisible));
                    OnPropertyChanged(nameof(MaxScanArea));
                    OnPropertyChanged(nameof(IsScanAreaSelectionVisible));
                    OnPropertyChanged(nameof(IsScanAreaAlignmentVisible));
                });
                break;
            case nameof(ScanOptions.ColorMode):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(IsColorModeColor));
                    OnPropertyChanged(nameof(IsColorModeGrayscale));
                    OnPropertyChanged(nameof(IsColorModeMonochrome));
                });
                break;
            case nameof(ScanOptions.TargetFormat):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(TargetFormat));
                });
                break;
            case nameof(ScanOptions.Resolution):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(SelectedResolution));
                });
                break;
            case nameof(ScanOptions.Brightness):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(CanResetBrightness));
                });
                break;
            case nameof(ScanOptions.Contrast):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(CanResetContrast));
                });
                break;
            case nameof(ScanOptions.ScanArea):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(IsAutoCropDisabled));
                    OnPropertyChanged(nameof(IsAutoCropSingle));
                    OnPropertyChanged(nameof(IsAutoCropSingleSupported));
                    OnPropertyChanged(nameof(IsAutoCropMulti));
                    OnPropertyChanged(nameof(IsAutoCropMultiSupported));
                    OnPropertyChanged(nameof(IsPaperSizeAreaSelected));
                    OnPropertyChanged(nameof(IsScanAreaAlignmentVisible));
                    OnPropertyChanged(nameof(IsTopLeftScanCornerSelected));
                    OnPropertyChanged(nameof(IsTopRightScanCornerSelected));
                    OnPropertyChanged(nameof(IsBottomLeftScanCornerSelected));
                    OnPropertyChanged(nameof(IsBottomRightScanCornerSelected));
                    OnPropertyChanged(nameof(SelectedOrientation));
                    OnPropertyChanged(nameof(SelectedOrientationInt));
                    OnPropertyChanged(nameof(SelectedPaperSize));
                    ApplyScanAreaToComboBox(ScanOptions.ScanArea);
                });
                
                if (ScanOptions.ScanArea is PaperSizeArea paperSizeArea)
                    paperSizeArea.PropertyChanged += PaperSizeArea_PropertyChanged;
                break;
        }
    }

    private void PaperSizeArea_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsTopLeftScanCornerSelected));
        OnPropertyChanged(nameof(IsTopRightScanCornerSelected));
        OnPropertyChanged(nameof(IsBottomLeftScanCornerSelected));
        OnPropertyChanged(nameof(IsBottomRightScanCornerSelected));
        OnPropertyChanged(nameof(SelectedOrientation));
        OnPropertyChanged(nameof(SelectedOrientationInt));
    }

    private void ButtonPageList_Click(object sender, RoutedEventArgs e)
    {
        ExpandPageListRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void ComboBoxScanners_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 && e.RemovedItems.Count == 0)
            return;

        if (ComboBoxScanners.SelectedItem == null)
        {
            ViewModel.SelectedScanner = null;
            return;
        }

        IScanningDevice? selectedDevice = ((ComboBoxItem)ComboBoxScanners.SelectedItem).Tag as IScanningDevice;
        if (selectedDevice != null)
        {
            // apply scanner selection
            ViewModel.SelectedScanner = (IScanningDevice)((ComboBoxItem)e.AddedItems[0]).Tag;
        }
        else if ((ComboBoxItem)e.AddedItems[0] == ComboBoxItemManageScanners)
        {
            // restore previous selection
            if (ViewModel.SelectedScanner != null)
                ComboBoxScanners.SelectedItem = ComboBoxScanners.Items.FirstOrDefault(x => ((ComboBoxItem)x).Tag == ViewModel.SelectedScanner);
            else
                ComboBoxScanners.SelectedIndex = ComboBoxScanners.Items.Count - invalidScannerItems;

            // open scanner settings
            ViewModel.SentryService?.TrackEvent(AnalyticsEvent.ManageScannersOpened);
            await Launcher.LaunchUriAsync(new Uri("ms-settings:printers"));
        }
    }

    private void Scanners_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    if (e.NewItems == null) return;

                    for (int i = 0; i < e.NewItems.Count; i++)
                    {
                        if (e.NewItems[i] is not IScanningDevice scanningDevice)
                            continue;

                        if (ComboBoxScanners.Items.Any(x => ((ComboBoxItem)x).Tag == scanningDevice))
                            continue;

                        ComboBoxItem item = CreateScannerComboBoxItem(scanningDevice);
                        ComboBoxScanners.Items.Insert(e.NewStartingIndex + i, item);

                        // select if necessary
                        if (ViewModel.SelectedScanner == e.NewItems[i])
                        {
                            ComboBoxScanners.SelectedItem = item;
                        }
                    }
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    if (e.OldItems == null) return;
                    foreach (IScanningDevice oldItem in e.OldItems)
                    {
                        // find corresponding ComboBoxItem
                        ComboBoxItem? item = null;
                        for (int i = 0; i < ComboBoxScanners.Items.Count - 1; i++)
                        {
                            if (((FrameworkElement)ComboBoxScanners.Items[i]).Tag == oldItem)
                            {
                                item = (ComboBoxItem)ComboBoxScanners.Items[i];
                                break;
                            }
                        }

                        if (item != null)
                        {
                            ComboBoxScanners.Items.Remove(item);
                        }

                        if (ComboBoxScanners.SelectedIndex == -1)
                        {
                            ComboBoxScanners.SelectedIndex = 0;
                        }
                    }
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                    if (e.NewItems == null || e.OldItems == null) return;                        
                    for (int i = 0; i < e.OldItems.Count; i++)
                    {
                        if (e.NewItems[i] is not IScanningDevice scanningDevice)
                            continue;

                        ComboBoxItem item = CreateScannerComboBoxItem(scanningDevice);
                        ComboBoxScanners.Items[e.OldStartingIndex + i] = item;
                    }
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                    var movedItem = ComboBoxScanners.Items[e.OldStartingIndex];
                    ComboBoxScanners.Items.RemoveAt(e.OldStartingIndex);
                    ComboBoxScanners.Items.Insert(e.NewStartingIndex, movedItem);
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    while (ComboBoxScanners.Items.Count > invalidScannerItems)
                    {
                        ComboBoxScanners.Items.RemoveAt(0);
                    }
                    break;
            }
            ScannerCount = ViewModel.Scanners.Count;
        });
    }

    private ComboBoxItem CreateScannerComboBoxItem(IScanningDevice device)
    {
        StackPanel stackPanel = (StackPanel)DataTemplateScanner.LoadContent();
        stackPanel.DataContext = device;
        return new ComboBoxItem()
        {
            Content = stackPanel,
            Tag = device
        };
    }

    private void ComboBoxScanners_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
#if DEBUG
        FlyoutBase.ShowAttachedFlyout(ComboBoxScanners);
#endif
    }

    private void ComboBoxResolution_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
    {
        if (int.TryParse(args.Text, out int intValue))
        {
            // entered pure number, try to apply it
            ScanResolution? resolution = Resolutions.FirstOrDefault((x) => x.Resolution.DpiX == intValue);
            if (resolution != null)
            {
                // found corresponding resolution
                SelectedResolution = resolution;
            }
            else
            {
                // no resolution for number, find the closest available one
                resolution = Resolutions.Aggregate((x, y) => Math.Abs(x.Resolution.DpiX - intValue) < Math.Abs(y.Resolution.DpiX - intValue) ? x : y);
                SelectedResolution = resolution;
            }
        }
        else
        {
            args.Handled = true;
        }

        // ensure ComboBox text reflects selected resolution, even if it didn't change
        OnPropertyChanged(nameof(SelectedResolution));
    }

    private void ComboBoxResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 1)
        {
            SelectedResolution = (ScanResolution)e.AddedItems[0];
        }
    }

    private void ScrollViewerContent_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanScroll));
    }

    private void StackPanelContent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanScroll));
    }

    private void SliderBrightness_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        ViewModel.ResetBrightnessCommand.Execute(null);
    }

    private void SliderContrast_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        ViewModel.ResetContrastCommand.Execute(null);
    }

    private void ComboBoxScanners_Loading(FrameworkElement sender, object args)
    {
        if (ViewModel.SelectedScanner == null)
            ComboBoxScanners.SelectedIndex = ComboBoxScanners.Items.Count - invalidScannerItems;
    }

    private void ComboBoxScanners_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedScanner == null)
            ComboBoxScanners.SelectedIndex = ComboBoxScanners.Items.Count - invalidScannerItems;
    }

    private void ComboBoxScanArea_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ComboBoxItem? item = ComboBoxScanArea.SelectedItem as ComboBoxItem;
        if (item == null)
            return;
        
        if (item == ComboBoxItemScanEverything)
        {
            ScanOptions.ScanArea = null;
        }
        else if (item == ComboBoxItemAutoCropSingle)
        {
            IsAutoCropSingle = true;
        }
        else if (item == ComboBoxItemAutoCropMulti)
        {
            IsAutoCropMulti = true;
        }
        else if (item.Tag is PaperSize paperSize)
        {
            if (ScanOptions.ScanArea is PaperSizeArea paperSizeArea)
            {
                paperSizeArea.PaperSize = paperSize;
            }
            else
            {
                ScanOptions.ScanArea = new PaperSizeArea
                {
                    PaperSize = paperSize
                };
            }
        }
        else if (item == ComboBoxItemPreviewSelection && ScanOptions.ScanArea is not PreviewSelectionArea)
        {
            ComboBoxScanArea.SelectedItem = ComboBoxItemScanEverything;
            ViewModel.ShowPreviewDialogCommand.Execute(null);
        }

        OnPropertyChanged(nameof(SelectedPaperSize));
    }

    private void FrameComboBoxScanners_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        foreach (ComboBoxItem item in ComboBoxScanners.Items)
        {
            if (item.Content is not MenuFlyoutSeparator)
                item.MaxWidth = e.NewSize.Width;
        }
    }

    private void ApplyScanAreaToComboBox(ScanArea? scanArea)
    {
        if (ComboBoxScanArea == null)
            return;

        if (scanArea == null)
        {
            ComboBoxScanArea.SelectedItem = ComboBoxItemScanEverything;
        }
        else if (scanArea is AutoCropArea autoCropArea)
        {
            if (autoCropArea.AutoCropMode == ScannerAutoCropMode.Disabled)
            {
                ComboBoxScanArea.SelectedItem = ComboBoxItemScanEverything;
            }
            else if (autoCropArea.AutoCropMode == ScannerAutoCropMode.SingleRegion)
            {
                ComboBoxScanArea.SelectedItem = ComboBoxItemAutoCropSingle;
            }
            else if (autoCropArea.AutoCropMode == ScannerAutoCropMode.MultipleRegions)
            {
                ComboBoxScanArea.SelectedItem = ComboBoxItemAutoCropMulti;
            }
        }
        else if (scanArea is PaperSizeArea paperSizeArea)
        {
            foreach (ComboBoxItem item in ComboBoxScanArea.Items)
            {
                if (item.Tag is PaperSize paperSize && paperSize == paperSizeArea.PaperSize)
                {
                    ComboBoxScanArea.SelectedItem = item;
                    break;
                }
            }
        }
        else if (scanArea is PreviewSelectionArea)
        {
            ComboBoxScanArea.SelectedItem = ComboBoxItemPreviewSelection;
        }
    }

    private void ComboBoxScanArea_Loading(FrameworkElement sender, object args)
    {
        ApplyScanAreaToComboBox(ViewModel.ScanOptions.ScanArea);
    }

    private void GridContent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ComboBoxFileFormats.MaxWidth = e.NewSize.Width - 40;
        foreach (ComboBoxItem item in ComboBoxFileFormats.Items)
        {
            item.MaxWidth = e.NewSize.Width - 40;
        }
    }

    private void FlyoutScanAreaAlignment_Opening(object sender, object e)
    {
        IsScanAreaAlignmentFlyoutOpen = true;
    }

    private void FlyoutScanAreaAlignment_Closed(object sender, object e)
    {
        IsScanAreaAlignmentFlyoutOpen = false;
    }
}
