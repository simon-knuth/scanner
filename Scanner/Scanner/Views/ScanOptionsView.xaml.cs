using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Models.Interfaces;
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


namespace Scanner.Views
{
    [ObservableObjectAttribute]
    public sealed partial class ScanOptionsView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////        
        #region Events
        public event EventHandler? ExpandPageListRequested;
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
                if (ViewModel.ScanOptions.TargetFormat == Models.TargetFormat.None)
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

        #region Auto crop
        public bool IsAutoCropDisabled
        {
            get => ViewModel.ScanOptions.AutoCropMode == ScannerAutoCropMode.Disabled;
            set
            {
                if (value)
                {
                    ViewModel.ScanOptions.AutoCropMode = ScannerAutoCropMode.Disabled;
                }
            }
        }

        public bool IsAutoCropSingle
        {
            get => ViewModel.ScanOptions.AutoCropMode == ScannerAutoCropMode.SingleRegion;
            set
            {
                if (value)
                {
                    ViewModel.ScanOptions.AutoCropMode = ScannerAutoCropMode.SingleRegion;
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
            get => ViewModel.ScanOptions.AutoCropMode == ScannerAutoCropMode.MultipleRegions;
            set
            {
                if (value)
                {
                    ViewModel.ScanOptions.AutoCropMode = ScannerAutoCropMode.MultipleRegions;
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

        #region Brightness & Contrast
        public bool CanResetBrightness => ViewModel.ScanOptions.Brightness != 0;
        public bool CanResetContrast => ViewModel.ScanOptions.Contrast != 0;
        #endregion

        public bool CanScroll => ScrollViewerContent.ScrollableHeight > 0;

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

            ViewModel.PropertyChanging += ViewModel_PropertyChanging;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.Scanners.CollectionChanged += Scanners_CollectionChanged;
            ViewModel.ScanOptions.PropertyChanged += ScanOptions_PropertyChanged;
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
                                ComboBoxScanners.SelectedIndex = ComboBoxScanners.Items.Count - 1;
                            }
                        }
                    });
                    break;
                case nameof(ViewModel.ScanOptions):
                    if (ViewModel.ScanOptions != null)
                    {
                        ViewModel.ScanOptions.PropertyChanged += ScanOptions_PropertyChanged;
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

                        OnPropertyChanged(nameof(ScanOptions));
                    });
                    break;
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
                case nameof(ScanOptions.AutoCropMode):
                    this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        OnPropertyChanged(nameof(IsAutoCropDisabled));
                        OnPropertyChanged(nameof(IsAutoCropSingle));
                        OnPropertyChanged(nameof(IsAutoCropSingleSupported));
                        OnPropertyChanged(nameof(IsAutoCropMulti));
                        OnPropertyChanged(nameof(IsAutoCropMultiSupported));
                    });
                    break;
            }
        }

        private void ComboBoxFileFormats_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            foreach (ComboBoxItem item in ComboBoxFileFormats.Items)
            {
                item.MaxWidth = e.NewSize.Width;
            }
        }

        private void GridContent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ComboBoxFileFormats.MaxWidth = Math.Max(0, e.NewSize.Width - 40);
        }

        private void ButtonPageList_Click(object sender, RoutedEventArgs e)
        {
            ExpandPageListRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ComboBoxScanners_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            
            // apply scanner selection
            ViewModel.SelectedScanner = (IScanningDevice)((ComboBoxItem)e.AddedItems[0]).Tag;
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
                        while (ComboBoxScanners.Items.Count > 1)
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
    }
}
