using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static Scanner.Helpers.Helpers;


namespace Scanner.Views
{
    [ObservableObjectAttribute]
    public sealed partial class ProjectView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Events
        public event EventHandler? ExpandPageListRequested;
        public event EventHandler? IsExpandedChanged;
        #endregion

        #region Dependency Properties
        public static readonly DependencyProperty CanExpandPageListProperty =
            DependencyProperty.Register(nameof(CanExpandPageList), typeof(bool), typeof(ProjectView), null);

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ProjectView),
            new PropertyMetadata(false, OnIsExpandedChanged));
        #endregion

        public bool CanExpandPageList
        {
            get => (bool)GetValue(CanExpandPageListProperty);
            set => SetValue(CanExpandPageListProperty, value);
        }

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set
            {
                SetValue(IsExpandedProperty, value);
            }
        }

        [ObservableProperty]
        private double projectFlyoutWidth;

        [ObservableProperty]
        private bool isHoveringCarousel;

        private bool isCarouselScrollSelectionDisabled;

        private bool showEntranceExitAnimations;

        private ScrollViewer? carouselScrollViewer;

        private bool isFileNameTextBoxFocused;
        private bool isTextBoxDiscardingUserInput;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ProjectView()
        {
            this.InitializeComponent();

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.ProjectService.PropertyChanged += ProjectService_PropertyChanged;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.FileName):
                    // suppress change while user is typing
                    if (!isFileNameTextBoxFocused && TextBoxProjectName != null)
                    {
                        TextBoxProjectName.Text = ViewModel.FileName;
                    }
                    break;
            }
        }

        private void ProjectService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IProjectService.SelectedPage):
                    this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, DiscardProjectNameInputIfFocused);
                    break;
            }
        }

        private static void OnIsExpandedChanged(DependencyObject source, DependencyPropertyChangedEventArgs args)
        {
            if (source is ProjectView view)
            {
                view.IsExpandedChanged?.Invoke(view, EventArgs.Empty);
            }
        }

        private void ButtonMore_Click(object sender, RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(GridHeader);
        }

        private void GridHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ProjectFlyoutWidth = e.NewSize.Width - 20;
        }

        private void ButtonRotate_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            FlyoutBase.ShowAttachedFlyout(ButtonRotate);
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(500);
            showEntranceExitAnimations = true;
        }

        private void ControlAnimated_Loading(FrameworkElement sender, object args)
        {
            // prevent animations during application startup
            if (!showEntranceExitAnimations)
            {
                Implicit.SetShowAnimations(sender, new ImplicitAnimationSet());
                Implicit.SetHideAnimations(sender, new ImplicitAnimationSet());
            }
        }

        private void TextBoxProjectName_GotFocus(object sender, RoutedEventArgs e)
        {
            isFileNameTextBoxFocused = true;
            ((TextBox)sender).SelectAll();
        }

        private void GridViewItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            GridViewItem gridViewItem = (GridViewItem)sender;

            Rectangle? innerSelectionIndicator = ((FrameworkElement)sender).FindDescendant("RectangleSelectionIndicatorInner") as Rectangle;
            Rectangle? outerSelectionIndicator = ((FrameworkElement)sender).FindDescendant("RectangleSelectionIndicatorOuter") as Rectangle;
            if (innerSelectionIndicator != null && outerSelectionIndicator != null)
            {
                if (gridViewItem.IsSelected)
                {
                    innerSelectionIndicator.StrokeThickness = 3;
                    outerSelectionIndicator.StrokeThickness = 2;
                }
                else
                {
                    innerSelectionIndicator.StrokeThickness = 1;
                    outerSelectionIndicator.StrokeThickness = 0;
                }
            }
        }

        private void GridViewItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            GridViewItem gridViewItem = (GridViewItem)sender;

            Rectangle? innerSelectionIndicator = ((FrameworkElement)sender).FindDescendant("RectangleSelectionIndicatorInner") as Rectangle;
            Rectangle? outerSelectionIndicator = ((FrameworkElement)sender).FindDescendant("RectangleSelectionIndicatorOuter") as Rectangle;
            if (innerSelectionIndicator != null && outerSelectionIndicator != null)
            {
                if (gridViewItem.IsSelected)
                {
                    innerSelectionIndicator.StrokeThickness = 3;
                    outerSelectionIndicator.StrokeThickness = 2;
                }
                else
                {
                    innerSelectionIndicator.StrokeThickness = 0;
                    outerSelectionIndicator.StrokeThickness = 0;
                }
            }
        }

        private void OnIsSelectedChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is GridViewItem gridViewItem)
            {
                Rectangle? innerSelectionIndicator = ((FrameworkElement)sender).FindDescendant("RectangleSelectionIndicatorInner") as Rectangle;
                Rectangle? outerSelectionIndicator = ((FrameworkElement)sender).FindDescendant("RectangleSelectionIndicatorOuter") as Rectangle;
                TextBlock? pageNumber = ((FrameworkElement)sender).FindDescendant("TextBlockPageNumber") as TextBlock;
                if (innerSelectionIndicator != null && outerSelectionIndicator != null && pageNumber != null)
                {
                    if (gridViewItem.IsSelected)
                    {
                        innerSelectionIndicator.Stroke = Application.Current.Resources["ControlSolidFillColorDefaultBrush"] as Brush;
                        innerSelectionIndicator.StrokeThickness = 3;
                        outerSelectionIndicator.StrokeThickness = 2;
                        pageNumber.Foreground = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as SolidColorBrush;
                    }
                    else
                    {
                        innerSelectionIndicator.Stroke = Application.Current.Resources["ControlStrokeColorOnAccentTertiaryBrush"] as Brush;
                        innerSelectionIndicator.StrokeThickness = 0;
                        outerSelectionIndicator.StrokeThickness = 0;
                        pageNumber.Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as SolidColorBrush;
                    }
                }
            }
        }

        private void GridViewItem_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewItem gridViewItem)
            {
                if (gridViewItem.IsSelected)
                {
                    Rectangle? innerSelectionIndicator = ((FrameworkElement)sender).FindDescendant("RectangleSelectionIndicatorInner") as Rectangle;
                    Rectangle? outerSelectionIndicator = ((FrameworkElement)sender).FindDescendant("RectangleSelectionIndicatorOuter") as Rectangle;
                    TextBlock? pageNumber = ((FrameworkElement)sender).FindDescendant("TextBlockPageNumber") as TextBlock;
                    if (innerSelectionIndicator != null && outerSelectionIndicator != null && pageNumber != null)
                    {
                        innerSelectionIndicator.Stroke = Application.Current.Resources["ControlSolidFillColorDefaultBrush"] as Brush;
                        innerSelectionIndicator.StrokeThickness = 3;
                        outerSelectionIndicator.StrokeThickness = 2;
                        pageNumber.Foreground = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as SolidColorBrush;
                    }
                }

                long token = gridViewItem.RegisterPropertyChangedCallback(GridViewItem.IsSelectedProperty, OnIsSelectedChanged);
                RoutedEventHandler? handler = null;
                handler = new RoutedEventHandler((s, e) =>
                {
                    if (s is GridViewItem item)
                    {
                        item.UnregisterPropertyChangedCallback(GridViewItem.IsSelectedProperty, token);
                        item.Unloaded -= handler;
                    }
                });
                gridViewItem.Unloaded += handler;
            }
        }

        private void GridCarousel_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            IsHoveringCarousel = true;
        }

        private void GridCarousel_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            IsHoveringCarousel = false;
        }

        private void GridViewCarousel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // scroll to item
                GridViewItem? item = ((GridView)sender).ContainerFromItem(e.AddedItems[0]) as GridViewItem;
                if (item != null)
                {
                    item.StartBringIntoView(new BringIntoViewOptions
                    {
                        AnimationDesired = true,
                        HorizontalAlignmentRatio = 0.5
                    });
                }
            }
            catch (Exception) { }
        }

        private void GridViewCarousel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // set padding
            GridViewCarousel.Padding = new Thickness(e.NewSize.Width / 2 - 32, 4, e.NewSize.Width / 2 - 32, 8);
        }

        private void ScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (e.IsIntermediate) return;

            // select centered item
            ScrollViewer? scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null && GridViewCarousel != null)
            {
                int index = Convert.ToInt32(Math.Round(scrollViewer.HorizontalOffset / 64));
                GridViewCarousel.SelectedIndex = index;
            }
        }

        private void ItemsStackPanelCarousel_Loaded(object sender, RoutedEventArgs e)   // ScrollViewer access more reliable than in GridView's Loaded event
        {
            try
            {
                carouselScrollViewer = VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(GridViewCarousel, 0), 0) as ScrollViewer;

                if (carouselScrollViewer != null)
                {
                    // enable carousel snap points
                    carouselScrollViewer.HorizontalSnapPointsType = SnapPointsType.Mandatory;
                    carouselScrollViewer.HorizontalSnapPointsAlignment = SnapPointsAlignment.Center;

                    // scroll to selected item
                    if (GridViewCarousel.SelectedItem != null)
                    {
                        carouselScrollViewer.ScrollToHorizontalOffset(64 * GridViewCarousel.SelectedIndex);
                    }

                    // enable carousel scroll selection
                    carouselScrollViewer.ViewChanged += ScrollViewer_ViewChanged;
                }
            }
            catch (Exception) { }
        }

        private void GridViewPageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // scroll to item
            try
            {
                GridViewPageList.ScrollIntoView(GridViewPageList.SelectedItem);
            }
            catch (Exception) { }
        }

        private void GridViewPageList_Loaded(object sender, RoutedEventArgs e)
        {
            if (GridViewPageList.SelectedItem != null)
            {
                GridViewPageList.ScrollIntoView(GridViewPageList.SelectedItem);
            }
        }

        private void ButtonPageList_Click(object sender, RoutedEventArgs e)
        {
            ExpandPageListRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ItemsStackPanelCarousel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // scroll carousel to end
            this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                try
                {
                    if (carouselScrollViewer != null && ViewModel.CurrentProject != null
                        && ViewModel.ProjectService.SelectedPage.Index == ViewModel.CurrentProject.Pages.Count - 1)
                    {
                        carouselScrollViewer.ChangeView(ViewModel.CurrentProject.Pages.Count * 64, null, null);
                    }
                }
                catch (Exception) { }
            });
        }

        private void GridViewPageList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // adjust padding to center items
            GridView gridView = (GridView)sender;
            double padding = e.NewSize.Width % 108;
            padding = padding / 2;
            if (padding < 12)
            {
                padding += 54;
            }
            padding -= 1;       // buffer for XAML rounding
            padding = Math.Floor(padding);

            gridView.Padding = new Thickness(padding, 12, padding, 8);
        }

        private void MenuFlyoutItemRename_Click(object sender, RoutedEventArgs e)
        {
            TextBoxProjectName.Focus(FocusState.Programmatic);
        }

        private void TextBoxProjectName_LostFocus(object sender, RoutedEventArgs e)
        {
            isFileNameTextBoxFocused = false;
            if (ViewModel.CurrentProject == null) return;

            if (isTextBoxDiscardingUserInput)
            {
                // discard and restore file name
                TextBoxProjectName.Text = ViewModel.FileName;
                isTextBoxDiscardingUserInput = false;
            }
            else
            {
                // update file name
                ViewModel.FileName = TextBoxProjectName.Text + TargetFormatToFileExtension(ViewModel.CurrentProject.Format);
            }

            // scroll TextBox to beginning
            ScrollViewer? scrollViewer = TextBoxProjectName.FindDescendant<ScrollViewer>();
            if (scrollViewer != null)
            {
                scrollViewer.ChangeView(0, null, null);
            }
        }

        private void TextBoxProjectName_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Enter:
                case Windows.System.VirtualKey.Accept:
                    /// focus other control, file name will be applied in <see cref="TextBoxProjectName_LostFocus(object, RoutedEventArgs)"/>
                    if (IsExpanded)
                    {
                        ButtonShowInFileExplorer.Focus(FocusState.Pointer);
                    }
                    else
                    {
                        ButtonShowInFileExplorer.Focus(FocusState.Pointer);
                    }
                    break;
                case Windows.System.VirtualKey.Escape:
                case Windows.System.VirtualKey.Cancel:
                    DiscardProjectNameInputIfFocused();
                    break;
            }
        }

        private void DiscardProjectNameInputIfFocused()
        {
            if (!isFileNameTextBoxFocused) return;

            isTextBoxDiscardingUserInput = true;

            /// focus other control, file name will be restored in <see cref="TextBoxProjectName_LostFocus(object, RoutedEventArgs)"/>
            if (IsExpanded)
            {
                ButtonShowInFileExplorer.Focus(FocusState.Pointer);
            }
            else
            {
                ButtonShowInFileExplorer.Focus(FocusState.Pointer);
            }
        }

        private void TextBoxProjectName_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            // block invalid chars
            if (!string.IsNullOrEmpty(args.NewText))
            {
                foreach (char invalidChar in System.IO.Path.GetInvalidFileNameChars())
                {
                    if (args.NewText.Contains(invalidChar))
                    {
                        args.Cancel = true;
                        return;
                    }
                }
            }
        }
    }
}
