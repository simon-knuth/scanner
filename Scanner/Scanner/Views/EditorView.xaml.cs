using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualBasic.FileIO;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Resources.Strings;
using Scanner.Services.Interfaces;
using Scanner.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using static Scanner.Helpers.Helpers;


namespace Scanner.Views
{
    [ObservableObjectAttribute]
    public sealed partial class EditorView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Constants
        private const float minZoomFactor = 1.0f;
        private const float maxZoomFactor = 2.5f;
        #endregion

        [ObservableProperty]
        private double pageWidth;

        [ObservableProperty]
        private double pageHeight;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FriendlyPageZoomFactor))]
        [NotifyPropertyChangedFor(nameof(IsToolbarBackgroundVisible))]
        [NotifyPropertyChangedFor(nameof(CanZoomIn))]
        [NotifyPropertyChangedFor(nameof(CanZoomOut))]
        private float pageZoomFactor = 1.0f;

        public string FriendlyPageZoomFactor => string.Format(GetLocalized(ResourcesExtension.KeyEnum.TextZoomFactor), PageZoomFactor * 100);

        public bool IsToolbarBackgroundVisible => PageZoomFactor > 1.0f || IsCropping || IsDrawing || ScrollViewerMainEditingControls?.ScrollableWidth > 0;

        [ObservableProperty]
        private bool isHoveringZoomControls;

        public bool CanZoomIn => PageZoomFactor < maxZoomFactor - 0.009f;
        public bool CanZoomOut => PageZoomFactor > minZoomFactor + 0.009f;

        private bool isCropping;
        public bool IsCropping
        {
            get => isCropping;
            set
            {
                if (SetProperty(ref isCropping, value))
                {
                    ViewModel.ProjectService.IsEditing = value;

                    OnPropertyChanged(nameof(IsToolbarBackgroundVisible));
                    OnPropertyChanged(nameof(IsEditingExperienceActive));
                }
            }
        }

        private bool isDrawing;
        public bool IsDrawing
        {
            get => isDrawing;
            set
            {
                if (SetProperty(ref isDrawing, value))
                {
                    ViewModel.ProjectService.IsEditing = value;

                    OnPropertyChanged(nameof(IsToolbarBackgroundVisible));
                    OnPropertyChanged(nameof(IsEditingExperienceActive));
                }
            }
        }

        public bool IsEditingExperienceActive => IsCropping || IsDrawing;

        public bool IsFilterNone
        {
            get
            {
                if (ViewModel.ProjectService.SelectedPage is ImagePage imagePage
                    && imagePage.Filter == ImageFilter.None)
                {
                    return true;
                }
                return false;
            }
        }

        public bool IsFilterGrayscale
        {
            get
            {
                if (ViewModel.ProjectService.SelectedPage is ImagePage imagePage
                    && imagePage.Filter == ImageFilter.Grayscale)
                {
                    return true;
                }
                return false;
            }
        }

        public bool IsFilterMonochrome
        {
            get
            {
                if (ViewModel.ProjectService.SelectedPage is ImagePage imagePage
                    && imagePage.Filter == ImageFilter.Monochrome)
                {
                    return true;
                }
                return false;
            }
        }

        public bool IsFilterNoneAvailable
        {
            get
            {
                if (ViewModel.ProjectService.SelectedPage is ImagePage imagePage)
                {
                    return imagePage.AvailableFilters.Contains(ImageFilter.None);
                }
                return false;
            }
        }

        public bool IsFilterGrayscaleAvailable
        {
            get
            {
                if (ViewModel.ProjectService.SelectedPage is ImagePage imagePage)
                {
                    return imagePage.AvailableFilters.Contains(ImageFilter.Grayscale);
                }
                return false;
            }
        }

        public bool IsFilterMonochromeAvailable
        {
            get
            {
                if (ViewModel.ProjectService.SelectedPage is ImagePage imagePage)
                {
                    return imagePage.AvailableFilters.Contains(ImageFilter.Monochrome);
                }
                return false;
            }
        }

        [ObservableProperty]
        private bool isSimilarPagesFlyoutOpen;

        [ObservableProperty]
        private bool areSimilarPagesSelectedForCrop;

        public string ProjectNavigationIndicator => string.Format(GetLocalized(ResourcesExtension.KeyEnum.ProjectNavigationIndicator), ViewModel.ProjectService.SelectedPage?.PageNumber, ViewModel.ProjectService.TotalNumberOfPages);

        private ScrollViewer? _selectedItemScrollViewer;
        private ScrollViewer? selectedItemScrollViewer
        {
            get => _selectedItemScrollViewer;
            set
            {
                if (_selectedItemScrollViewer != null)
                {
                    _selectedItemScrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
                }

                _selectedItemScrollViewer = value;

                if (value != null)
                {
                    value.ViewChanged += ScrollViewer_ViewChanged;
                }
            }
        }

        private CoreInputDeviceTypes inkCanvasInputDeviceTypes => ViewModel.SettingsService.LastTouchDrawState ?
            CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Touch :
            CoreInputDeviceTypes.Pen;

        private VirtualizingStackPanel? flipViewPanel;

        private Dictionary<IProjectPage, CanvasControl> pageCanvases = [];


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public EditorView()
        {
            this.InitializeComponent();

            ViewModel.SettingsService.PropertyChanged += SettingsService_PropertyChanged;
            ViewModel.ProjectService.PropertyChanging += ProjectService_PropertyChanging;
            ViewModel.ProjectService.PropertyChanged += ProjectService_PropertyChanged;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ProjectService_PropertyChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IProjectService.SelectedPage):
                    if (ViewModel.ProjectService.SelectedPage != null)
                    {
                        ViewModel.ProjectService.SelectedPage.PropertyChanged -= SelectedPage_PropertyChanged;
                    }
                    break;
            }
        }


        private void ProjectService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IProjectService.SelectedPage):
                    OnPropertyChanged(nameof(IsFilterNone));
                    OnPropertyChanged(nameof(IsFilterGrayscale));
                    OnPropertyChanged(nameof(IsFilterMonochrome));
                    OnPropertyChanged(nameof(IsFilterNoneAvailable));
                    OnPropertyChanged(nameof(IsFilterGrayscaleAvailable));
                    OnPropertyChanged(nameof(IsFilterMonochromeAvailable));
                    OnPropertyChanged(nameof(ProjectNavigationIndicator));

                    if (ViewModel.ProjectService.SelectedPage != null)
                        ViewModel.ProjectService.SelectedPage.PropertyChanged += SelectedPage_PropertyChanged;
                    break;
                case nameof(IProjectService.CurrentProject):
                    OnPropertyChanged(nameof(ProjectNavigationIndicator));
                    break;
                case nameof(IProjectService.TotalNumberOfPages):
                    OnPropertyChanged(nameof(ProjectNavigationIndicator));
                    break;
            }
        }

        private void SelectedPage_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ImagePage.Filter):
                    OnPropertyChanged(nameof(IsFilterNone));
                    OnPropertyChanged(nameof(IsFilterGrayscale));
                    OnPropertyChanged(nameof(IsFilterMonochrome));
                    break;
            }
        }

        private void SettingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ISettingsService.SettingEditorOrientation):
                    _ = ApplyFlipViewOrientationAsync(SettingEditorOrientationToOrientation(ViewModel.SettingsService.SettingEditorOrientation));
                    break;
            }
        }

        private void ButtonRotate_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private async Task ApplyFlipViewOrientationAsync(Orientation orientation)
        {
            await this.RunOnUIThreadAndWaitAsync(DispatcherQueuePriority.Normal, () =>
            {
                if (flipViewPanel != null)
                {
                    flipViewPanel.Orientation = orientation;
                }

                // fix scrolling in vertical mode
                if (orientation == Orientation.Vertical)
                {
                    ((ScrollViewer)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(FlipViewPages, 0), 0)).HorizontalScrollMode = ScrollMode.Disabled;
                }
                else
                {
                    ((ScrollViewer)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(FlipViewPages, 0), 0)).HorizontalScrollMode = ScrollMode.Enabled;
                }
            });
        }

        private Orientation SettingEditorOrientationToOrientation(SettingEditorOrientation setting)
        {
            return setting switch
            {
                SettingEditorOrientation.Horizontal => Orientation.Horizontal,
                SettingEditorOrientation.Vertical => Orientation.Vertical,
                _ => Orientation.Horizontal,
            };
        }

        private void VirtualizingStackPanel_Loading(FrameworkElement sender, object args)
        {
            flipViewPanel = sender as VirtualizingStackPanel;
            _ = ApplyFlipViewOrientationAsync(SettingEditorOrientationToOrientation(ViewModel.SettingsService.SettingEditorOrientation));

            if (flipViewPanel != null)
            {
                foreach (FlipViewItem item in flipViewPanel.Children)
                {
                    ScrollViewer? scrollViewer = item.FindDescendant<ScrollViewer>();
                    if (item.IsSelected)
                    {
                        selectedItemScrollViewer = scrollViewer;
                        return;
                    }
                }
            }
        }

        private void ScrollViewerPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScrollViewer scrollViewer = (ScrollViewer)sender;

            PageWidth = scrollViewer.ViewportWidth;
            PageHeight = scrollViewer.ViewportHeight;
        }

        private void FlipViewPages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InitializeZoomProperties();
        }
        
        private void InitializeZoomProperties()
        {
            // reset zoom factor for unselected items
            if (flipViewPanel == null) return;

            selectedItemScrollViewer = null;
            PageZoomFactor = 1.0f;
            foreach (FlipViewItem item in flipViewPanel.Children)
            {
                ScrollViewer? scrollViewer = item.FindDescendant<ScrollViewer>();
                if (item.IsSelected)
                {
                    selectedItemScrollViewer = scrollViewer;
                }
                else
                {
                    scrollViewer?.ChangeView(null, null, 1.0f, true);
                }
            }
        }

        private void ScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (selectedItemScrollViewer == null || sender is not ScrollViewer scrollViewer) return;
            PageZoomFactor = selectedItemScrollViewer.ZoomFactor;
        }

        private void ButtonPageZoomFactor_Click(object sender, RoutedEventArgs e)
        {
            selectedItemScrollViewer?.ChangeView(null, null, 1.0f);
        }

        private void FlipViewPages_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeZoomProperties();
        }

        private void GridToolbarZoom_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            IsHoveringZoomControls = true;
        }

        private void GridToolbarZoom_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            IsHoveringZoomControls = false;
        }

        private void ButtonPageZoomFactorIncrease_Click(object sender, RoutedEventArgs e)
        {
            TryZoomScanAsync(0.5f, true);
        }

        private void ButtonPageZoomFactorDecrease_Click(object sender, RoutedEventArgs e)
        {
            TryZoomScanAsync(-0.5f, true);
        }

        private void FlipViewItem_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            InitializeZoomProperties();
        }

        private void FlipViewItemPage_Loaded(object sender, RoutedEventArgs e)
        {
            ((FlipViewItem)sender).RegisterPropertyChangedCallback(FlipViewItem.IsSelectedProperty, (s, e) =>
            {
                InitializeZoomProperties();
            });
        }

        private void TryZoomScanAsync(float change, bool animate)
        {
            if (selectedItemScrollViewer == null) return;

            float newFactor = selectedItemScrollViewer.ZoomFactor + change;
            if (newFactor > maxZoomFactor) newFactor = maxZoomFactor;
            if (newFactor < minZoomFactor) newFactor = minZoomFactor;

            try
            {
                // Calculate the center of the viewport in content coordinates before zooming
                double horizontalCenter = selectedItemScrollViewer.HorizontalOffset + (selectedItemScrollViewer.ViewportWidth / 2);
                double verticalCenter = selectedItemScrollViewer.VerticalOffset + (selectedItemScrollViewer.ViewportHeight / 2);

                // Preserve the center point correctly after zooming
                double scaleRatio = newFactor / selectedItemScrollViewer.ZoomFactor;

                double newHorizontalOffset = Math.Max((horizontalCenter * scaleRatio) - (selectedItemScrollViewer.ViewportWidth / 2), 0);
                double newVerticalOffset = Math.Max((verticalCenter * scaleRatio) - (selectedItemScrollViewer.ViewportHeight / 2), 0);

                selectedItemScrollViewer.ChangeView(newHorizontalOffset, newVerticalOffset, newFactor, !animate);
            }
            catch (Exception) { }
        }

        private async void CanvasPreview_CreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            IProjectPage? page = sender.DataContext as IProjectPage;
            if (page == null)
                return;

            sender.Tag = await CacheCanvasBitmapAsync(sender, page, null);
        }

        private void CanvasPreview_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            CanvasPageData? canvasPageData = sender.Tag as CanvasPageData;
            if (canvasPageData == null)
                return;

            args.DrawingSession.DrawImage(canvasPageData.Bitmap);

            sender.Width = canvasPageData.Bitmap.Size.Width;
            sender.Height = canvasPageData.Bitmap.Size.Height;
        }

        private void CanvasPreview_Unloaded(object sender, RoutedEventArgs e)
        {
            CanvasControl canvas = (CanvasControl)sender;
            CanvasPageData? canvasPageData = canvas.Tag as CanvasPageData;
            if (canvasPageData == null)
                return;

            canvasPageData.Bitmap.Dispose();
        }

        private async void CanvasPreview_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            CanvasControl canvas = (CanvasControl)sender;
            CanvasPageData? canvasPageData = canvas.Tag as CanvasPageData;

            if (!canvas.ReadyToDraw)
                return;

            // clear canvas to prevent wrong image from briefly being displayed during recycling
            canvas.Tag = null;
            canvas.Invalidate();

            try
            {
                IProjectPage page = (IProjectPage)canvas.DataContext;

                // discard old data
                if (canvasPageData != null)
                    canvasPageData.Bitmap.Dispose();

                // load new data
                if (page == null || page.PreviewBitmapUri == null)
                    return;

                canvas.Tag = await CacheCanvasBitmapAsync(canvas, page, canvasPageData?.Page);
            }
            finally
            {
                canvas.Invalidate();
            }
        }

        private async Task<CanvasPageData> CacheCanvasBitmapAsync(CanvasControl canvas, IProjectPage page, IProjectPage? previousPage)
        {
            if (previousPage != null)
            {
                previousPage.PropertyChanged -= Page_PropertyChanged;
                pageCanvases.Remove(previousPage);
            }

            pageCanvases[page] = canvas;
            page.PropertyChanged += Page_PropertyChanged;

            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(page.PreviewBitmapUri);

            // load the image file into a CanvasBitmap
            using IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();
            CanvasBitmap newBitmap = await CanvasBitmap.LoadAsync(canvas, stream);

            canvas.Width = newBitmap.Size.Width;
            canvas.Height = newBitmap.Size.Height;

            return new CanvasPageData(page, newBitmap);
        }

        private async void Page_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IProjectPage.PreviewBitmapUri))
                return;

            IProjectPage? page = sender as IProjectPage;
            if (page == null)
                return;

            CanvasControl canvas = pageCanvases[page];
            canvas.Tag = await CacheCanvasBitmapAsync(canvas, page, null);
        }

        private void ButtonCrop_Click(object sender, RoutedEventArgs e)
        {
            IsCropping = true;
        }

        private async void ImageCropper_Loading(FrameworkElement sender, object args)
        {
            if (ViewModel.CurrentProject == null)
                return;
            if (ViewModel.ProjectService.SelectedPage == null)
                return;

            // load image into ImageCropper
            ((CommunityToolkit.WinUI.Controls.ImageCropper)sender).AspectRatio = ViewModel.SelectedAspectRatioValue;
            await ((CommunityToolkit.WinUI.Controls.ImageCropper)sender).LoadImageFromFile(ViewModel.ProjectService.SelectedPage.SourceFile);
        }

        private void ButtonDiscardCrop_Click(object sender, RoutedEventArgs e)
        {
            IsCropping = false;
        }

        private void ToggleMenuFlyoutItemAspectRatio_Click(object sender, RoutedEventArgs e)
        {
            ToggleMenuFlyoutItem item = (ToggleMenuFlyoutItem)sender;

            // prevent unchecking
            if (!item.IsChecked)
                item.IsChecked = true;

            // apply selection
            AspectRatio aspectRatio = (AspectRatio)item.Tag;
            ViewModel.SelectedAspectRatio = aspectRatio;
        }

        private void MenuFlyoutItemCropAspectRatioFlip_Click(object sender, RoutedEventArgs e)
        {
            if (ImageCropper == null)
                return;

            ViewModel.SelectedAspectRatioValue = ImageCropper.CroppedRegion.Height / ImageCropper.CroppedRegion.Width;

            // fix aspect ratio locked after flipping custom
            if (ViewModel.SelectedAspectRatio == AspectRatio.Custom)
            {
                ViewModel.SelectedAspectRatioValue = null;
            }
        }

        private async void SplitButtonSaveCrop_Click(SplitButton sender, SplitButtonClickEventArgs args)
        {
            await SaveCropAsync(false);
        }

        private async void MenuFlyoutItemSaveCrop_Click(object sender, RoutedEventArgs e)
        {
            await SaveCropAsync(false);
        }

        private async Task SaveCropAsync(bool asCopy)
        {
            if (asCopy)
                await ViewModel.CropCurrentPageAsCopyAsyncCommand.ExecuteAsync(ImageCropper.CroppedRegion);
            else
                await ViewModel.CropCurrentPageAsyncCommand.ExecuteAsync(ImageCropper.CroppedRegion);

            IsCropping = false;
        }

        private void MenuFlyoutItemCropSimilarPages_Click(object sender, RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(GridCropToolbar);
        }

        private void CheckBoxCropSimilarPagesSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            ListViewCropSimilarPages.SelectAll();
        }

        private void CheckBoxCropSimilarPagesSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            ListViewCropSimilarPages.SelectedItem = null;
        }

        private void CheckBoxCropSimilarPagesSelectAll_Indeterminate(object sender, RoutedEventArgs e)
        {
            // prevent indeterminate state if caused by selecting CheckBox
            uint selectedItems = 0;
            foreach (ItemIndexRange range in ListViewCropSimilarPages.SelectedRanges)
            {
                selectedItems += range.Length;
            }

            if (selectedItems == ListViewCropSimilarPages.Items.Count)
            {
                CheckBoxCropSimilarPagesSelectAll.IsChecked = false;
            }
        }

        private void ListViewCropSimilarPagesCurrent_Loading(FrameworkElement sender, object args)
        {
            if (ViewModel.ProjectService.SelectedPage == null) return;

            ((ListView)sender).ItemsSource = new List<IProjectPage>([ViewModel.ProjectService.SelectedPage]);
            ((ListView)sender).SelectedIndex = 0;
        }

        private void ListViewCropSimilarPages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewCropSimilarPages.SelectedItems.Count == ListViewCropSimilarPages.Items.Count)
                CheckBoxCropSimilarPagesSelectAll.IsChecked = true;
            else if (ListViewCropSimilarPages.SelectedItems.Count == 0)
                CheckBoxCropSimilarPagesSelectAll.IsChecked = false;
            else
                CheckBoxCropSimilarPagesSelectAll.IsChecked = null;

            AreSimilarPagesSelectedForCrop = ListViewCropSimilarPages.SelectedItems.Count > 0;
        }

        private void ButtonCropSimilarPagesCancel_Click(object sender, RoutedEventArgs e)
        {
            FlyoutBase.GetAttachedFlyout(GridCropToolbar).Hide();
        }

        private async void ButtonCropSimilarPagesConfirm_Click(object sender, RoutedEventArgs e)
        {
            // collect pages
            List<IProjectPage> pages = ListViewCropSimilarPages.SelectedItems.OfType<IProjectPage>().ToList();
            if (ViewModel.ProjectService.SelectedPage != null)
                pages.Insert(0, ViewModel.ProjectService.SelectedPage);

            // crop
            FlyoutBase.GetAttachedFlyout(GridCropToolbar).Hide();
            await ViewModel.CropPagesAsyncCommand.ExecuteAsync((pages, ImageCropper.CroppedRegion));
        }

        private void FlyoutCropSimilarPages_Opened(object sender, object e)
        {
            IsSimilarPagesFlyoutOpen = true;
        }

        private void FlyoutCropSimilarPages_Closed(object sender, object e)
        {
            IsSimilarPagesFlyoutOpen = false;
        }

        private void MenuFlyoutItemCropSimilarPages_Loading(FrameworkElement sender, object args)
        {
            ((MenuFlyoutItem)sender).IsEnabled = ViewModel.AreSimilarPagesForCropAvailable;
        }

        private void ListViewCropSimilarPages_Loading(FrameworkElement sender, object args)
        {
            ((ListView)sender).ItemsSource = ViewModel.SimilarPagesForCrop;
        }

        private async void MenuFlyoutItemSaveCropAsCopy_Click(object sender, RoutedEventArgs e)
        {
            await SaveCropAsync(true);
        }

        private void ButtonDraw_Click(object sender, RoutedEventArgs e)
        {
            IsDrawing = true;
        }

        private void ButtonDiscardDraw_Click(object sender, RoutedEventArgs e)
        {
            IsDrawing = false;
        }

        private void ScrollViewerMainEditingControls_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsToolbarBackgroundVisible));
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private record CanvasPageData(IProjectPage Page, CanvasBitmap Bitmap);
    }
}
