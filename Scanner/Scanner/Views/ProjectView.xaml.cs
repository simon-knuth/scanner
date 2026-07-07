using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
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
using Scanner.Helpers;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services;
using Scanner.Services.Interfaces;
using Scanner.ViewModels;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using WinRT;
using WinRT.Interop;
using WinUIEx;
using static CommunityToolkit.WinUI.Animations.Expressions.ExpressionValues;
using static Scanner.Helpers.Helpers;


namespace Scanner.Views;

[ObservableRecipientAttribute]
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
            ViewModel.IsMultiSelect = false;
        }
    }

    [ObservableProperty]
    private double projectFlyoutWidth;

    [ObservableProperty]
    private bool isHoveringCarousel;

    public Thickness FileNameTextBoxPadding => ShowFileNameGenerationButton ? new Thickness(8, 4, 36, 4) : new Thickness(8, 4, 4, 4);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFileNameGenerationButton))]
    [NotifyPropertyChangedFor(nameof(FileNameTextBoxPadding))]
    private bool isFileNameTextBoxFocused;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFileNameGenerationButton))]
    [NotifyPropertyChangedFor(nameof(FileNameTextBoxPadding))]
    private bool isFileNameGenerationButtonFocused;

    public bool ShowFileNameGenerationButton => ViewModel.CurrentProject is PdfProject && ViewModel.CopilotRuntimeService.IsSupported &&
        (IsFileNameTextBoxFocused || IsFileNameGenerationButtonFocused || ViewModel.IsFileNameGenerationInProgress);

    public bool AreMultiSelectEditActionsAvailable => !ViewModel.ProjectService.IsProcessRunningOrEditing && ViewModel.IsMultiSelect && ViewModel.ProjectService.SelectedPagesCount > 0
        && ViewModel.ProjectService.SelectedPages != null && !ViewModel.ProjectService.SelectedPages.Any(x => x is not ImagePage);
    public bool IsMultiSelectExportAvailable => !ViewModel.ProjectService.IsProcessRunningOrEditing && ViewModel.IsMultiSelect && ViewModel.ProjectService.SelectedPagesCount > 0
        && ViewModel.CurrentProject?.IsPdf == true;

    public bool ShowTextBlockTotalPages => ViewModel.CurrentProject?.IsPdf == true || ViewModel.IsMultiSelect;

    public bool AreFilterNone
    {
        get
        {
            if (ViewModel.ProjectService.SelectedPages != null)
            {
                return ViewModel.ProjectService.SelectedPages.OfType<ImagePage>().All(x => x.Filter == ImageFilter.None);
            }
            return false;
        }
    }

    public bool AreFilterGrayscale
    {
        get
        {
            if (ViewModel.ProjectService.SelectedPages != null)
            {
                return ViewModel.ProjectService.SelectedPages.OfType<ImagePage>().All(x => x.Filter == ImageFilter.Grayscale);
            }
            return false;
        }
    }

    public bool AreFilterMonochrome
    {
        get
        {
            if (ViewModel.ProjectService.SelectedPages != null)
            {
                return ViewModel.ProjectService.SelectedPages.OfType<ImagePage>().All(x => x.Filter == ImageFilter.Monochrome);
            }
            return false;
        }
    }

    public bool IsFilterNoneAvailable
    {
        get
        {
            if (ViewModel.ProjectService.SelectedPages != null)
            {
                return ViewModel.ProjectService.SelectedPages.OfType<ImagePage>().All(x => x.AvailableFilters.Contains(ImageFilter.None));
            }
            return false;
        }
    }

    public bool IsFilterGrayscaleAvailable
    {
        get
        {
            if (ViewModel.ProjectService.SelectedPages != null)
            {
                return ViewModel.ProjectService.SelectedPages.OfType<ImagePage>().All(x => x.AvailableFilters.Contains(ImageFilter.Grayscale));
            }
            return false;
        }
    }

    public bool IsFilterMonochromeAvailable
    {
        get
        {
            if (ViewModel.ProjectService.SelectedPages != null)
            {
                return ViewModel.ProjectService.SelectedPages.OfType<ImagePage>().All(x => x.AvailableFilters.Contains(ImageFilter.Monochrome));
            }
            return false;
        }
    }

    public string SelectedPagesString => string.Format(GetLocalized(Scanner.Resources.Strings.ResourcesExtension.KeyEnum.SelectedPagesIndicator), ViewModel.ProjectService.SelectedPagesCount);
    public string TotalPagesString => string.Format(GetLocalized(Scanner.Resources.Strings.ResourcesExtension.KeyEnum.TotalPagesIndicator), ViewModel.ProjectService.TotalNumberOfPages);
    public string SelectedFileString => string.Format(GetLocalized(Scanner.Resources.Strings.ResourcesExtension.KeyEnum.SelectedFileIndicator), ViewModel.ProjectService.SelectedPage?.PageNumber, ViewModel.ProjectService.TotalNumberOfPages);

    public bool ShowActionExtentOptions => ViewModel.CurrentProject != null && !ViewModel.CurrentProject.IsPdf && ViewModel.CurrentProject.Pages.Count > 1;

    private bool showEntranceExitAnimations;

    private ScrollViewer? carouselScrollViewer;

    private bool isTextBoxDiscardingUserInput;

    private DragEventHandler gridViewDragOverHandler;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ProjectView()
    {
        this.InitializeComponent();
        Ioc.Default.GetService<ILogService>()?.Log.Information("View loaded");

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.ProjectService.PropertyChanged += ProjectService_PropertyChanged;
        ViewModel.PropertyChanging += ViewModel_PropertyChanging;
        ViewModel.SettingsService.PropertyChanged += SettingsService_PropertyChanged;

        WeakReferenceMessenger.Default.Register<InvokeShareUIMessage>(this, (r, m) => InvokeShareUI());
        gridViewDragOverHandler = new(GridViewDropZone_DragOver);
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
                if (!IsFileNameTextBoxFocused && TextBoxProjectName != null)
                {
                    TextBoxProjectName.Text = ViewModel.FileName;
                }
                break;
            case nameof(ViewModel.CurrentProject):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(ShowTextBlockTotalPages));
                    OnPropertyChanged(nameof(ShowFileNameGenerationButton));
                    OnPropertyChanged(nameof(FileNameTextBoxPadding));
                    OnPropertyChanged(nameof(ShowActionExtentOptions));
                    OnPropertyChanged(nameof(IsMultiSelectExportAvailable));
                });

                if (ViewModel.CurrentProject != null)
                {
                    ViewModel.CurrentProject.PagesAdded += CurrentProject_PagesAddedOrRemoved;
                    ViewModel.CurrentProject.PagesRemoved += CurrentProject_PagesAddedOrRemoved;
                }
                break;
            case nameof(ViewModel.IsMultiSelect):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, ApplyIsMultiSelect);
                break;
            case nameof(ViewModel.IsFileNameGenerationInProgress):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    if (!ViewModel.IsFileNameGenerationInProgress)
                        IsFileNameGenerationButtonFocused = false;

                    OnPropertyChanged(nameof(ShowFileNameGenerationButton));
                    OnPropertyChanged(nameof(FileNameTextBoxPadding));
                });
                break;
        }
    }

    private void CurrentProject_PagesAddedOrRemoved(object? sender, EventArgs e)
    {
        this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            OnPropertyChanged(nameof(ShowActionExtentOptions));
        });
    }

    private void ProjectService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IProjectService.SelectedPage):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    DiscardProjectNameInputIfFocused();
                    OnPropertyChanged(nameof(SelectedFileString));
                });
                break;
            case nameof(IProjectService.SelectedPages):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    OnPropertyChanged(nameof(AreFilterNone));
                    OnPropertyChanged(nameof(AreFilterGrayscale));
                    OnPropertyChanged(nameof(AreFilterMonochrome));
                    OnPropertyChanged(nameof(IsFilterNoneAvailable));
                    OnPropertyChanged(nameof(IsFilterGrayscaleAvailable));
                    OnPropertyChanged(nameof(IsFilterMonochromeAvailable));
                    OnPropertyChanged(nameof(AreMultiSelectEditActionsAvailable));

                    if (ViewModel.ProjectService.SelectedPages == null)
                        return;

                    // select items
                    foreach (IProjectPage page in ViewModel.ProjectService.SelectedPages)
                    {
                        GridViewItem? item = GridViewPageList.ContainerFromItem(page) as GridViewItem;

                        if (item == null)
                            continue;

                        item.IsSelected = true;
                    }
                });
                break;
            case nameof(IProjectService.SelectedPagesCount):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    OnPropertyChanged(nameof(SelectedPagesString));
                    OnPropertyChanged(nameof(SelectedFileString));
                    OnPropertyChanged(nameof(AreMultiSelectEditActionsAvailable));
                    OnPropertyChanged(nameof(IsMultiSelectExportAvailable));
                    OnPropertyChanged(nameof(AreFilterNone));
                    OnPropertyChanged(nameof(AreFilterGrayscale));
                    OnPropertyChanged(nameof(AreFilterMonochrome));
                    OnPropertyChanged(nameof(IsFilterNoneAvailable));
                    OnPropertyChanged(nameof(IsFilterGrayscaleAvailable));
                    OnPropertyChanged(nameof(IsFilterMonochromeAvailable));
                });
                break;
            case nameof(IProjectService.IsProcessRunningOrEditing):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    OnPropertyChanged(nameof(AreMultiSelectEditActionsAvailable));
                    OnPropertyChanged(nameof(IsMultiSelectExportAvailable));
                });
                break;
            case nameof(IProjectService.TotalNumberOfPages):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    OnPropertyChanged(nameof(TotalPagesString));
                    OnPropertyChanged(nameof(SelectedFileString));
                });
                break;
        }
    }

    private void ViewModel_PropertyChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ViewModel.CurrentProject):
                if (ViewModel.CurrentProject != null)
                {
                    ViewModel.CurrentProject.PagesAdded -= CurrentProject_PagesAddedOrRemoved;
                    ViewModel.CurrentProject.PagesRemoved -= CurrentProject_PagesAddedOrRemoved;
                }
                break;
        }
    }

    private void SettingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsService.SettingAnimations):
                showEntranceExitAnimations = ViewModel.SettingsService.SettingAnimations;
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
        KeyboardHookHelper.KeyPressed += KeyboardHookHelper_KeyPressed;

        await Task.Delay(500);
        showEntranceExitAnimations = ViewModel.SettingsService.SettingAnimations;
    }

    private void KeyboardHookHelper_KeyPressed(object? sender, Windows.System.VirtualKey key)
    {
        if (key == Windows.System.VirtualKey.F2)
            FocusProjectNameTextBox();

        if (key == Windows.System.VirtualKey.Escape)
            ViewModel.IsMultiSelect = false;

        if (key == Windows.System.VirtualKey.A
            && Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)
            && ViewModel.IsMultiSelect
            && GridViewPageList.IsEnabled)
            GridViewPageList.SelectAllSafe();
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
        IsFileNameTextBoxFocused = true;
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
                    innerSelectionIndicator.Stroke = Application.Current.Resources["SurfaceStrokeColorDefaultBrush"] as Brush;
                    innerSelectionIndicator.StrokeThickness = 0;
                    outerSelectionIndicator.StrokeThickness = 0;
                    pageNumber.Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as SolidColorBrush;
                }
            }
        }
    }

    private void GridCarousel_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch)
            return;

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
                    AnimationDesired = ViewModel.SettingsService.SettingAnimations,
                    HorizontalAlignmentRatio = 0.5
                });
            }
        }
        catch (Exception) { }
    }

    private void GridViewCarousel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not GridView gridView)
            return;

        // set padding
        gridView.Padding = new Thickness(e.NewSize.Width / 2 - 32, 4, e.NewSize.Width / 2 - 32, 8);
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
        // synchronize selection with ProjectService
        if (ViewModel.IsMultiSelect)
        {
            // add selected pages
            foreach (IProjectPage page in GridViewPageList.SelectedItems)
            {
                if (ViewModel.ProjectService.SelectedPages?.Contains(page) == false)
                {
                    ViewModel.ProjectService.SelectedPages.Add(page);
                }
            }

            // remove deselected pages
            if (ViewModel.ProjectService.SelectedPages != null)
            {
                for (int i = 0; i < ViewModel.ProjectService.SelectedPages.Count; i++)
                {
                    if (!GridViewPageList.SelectedItems.Contains(ViewModel.ProjectService.SelectedPages[i]))
                    {
                        ViewModel.ProjectService.SelectedPages.RemoveAt(i);
                        i--;
                    }
                }
            }
        }
        else
        {
            ViewModel.ProjectService.SelectedPage = GridViewPageList?.SelectedItem as IProjectPage;
        }

        // scroll to item
        try
        {
            GridViewPageList?.ScrollIntoView(GridViewPageList.SelectedItem);
        }
        catch (Exception) { }
    }

    private void GridViewPageList_Loaded(object sender, RoutedEventArgs e)
    {
        GridView gridView = (GridView)sender;

        gridView.SelectedItem = ViewModel.ProjectService.SelectedPage;
        if (gridView.SelectedItem != null)
        {
            gridView.ScrollIntoView(gridView.SelectedItem);
        }

        gridView.AddHandler(GridView.DragOverEvent, gridViewDragOverHandler, true);
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
                    && ViewModel.ProjectService.SelectedPage?.Index == ViewModel.Pages.Count - 1)
                {
                    carouselScrollViewer.ChangeView(ViewModel.Pages.Count * 64, null, null);
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
        if (padding < 8)
        {
            padding += 54;
        }
        padding -= 1;               // buffer for XAML rounding
        padding = Math.Floor(padding);

        gridView.Padding = new Thickness(padding, 12, padding, 8);
    }

    private void MenuFlyoutItemRename_Click(object sender, RoutedEventArgs e)
    {
        FocusProjectNameTextBox();
    }

    private void TextBoxProjectName_LostFocus(object sender, RoutedEventArgs e)
    {
        IsFileNameTextBoxFocused = false;
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
        if (!IsFileNameTextBoxFocused) return;

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

    private void SplitMenuFlyoutItemOpenWith_Loading(FrameworkElement sender, object args)
    {
        SplitMenuFlyoutItem parentItem = (SplitMenuFlyoutItem)sender;

        // clear list
        while (parentItem.Items.Count > 3)
        {
            parentItem.Items.RemoveAt(0);
        }

        // add items
        List<OpenWithTarget> reversed = [.. ViewModel.OpenWithTargets];
        reversed.Reverse();
        foreach (OpenWithTarget target in reversed)
        {
            MenuFlyoutItem item = new MenuFlyoutItem()
            {
                Text = target.AppInfo.DisplayInfo.DisplayName,
                Command = ViewModel.TryOpenWithAsyncCommand,
                CommandParameter = target.AppInfo,
            };

            // add logo
            if (target.Logo != null)
            {
                target.Logo.DecodePixelType = Microsoft.UI.Xaml.Media.Imaging.DecodePixelType.Logical;
                target.Logo.DecodePixelWidth = 32;
                target.Logo.DecodePixelHeight = 32;

                ImageIcon icon = new ImageIcon
                {
                    Source = target.Logo
                };

                item.Icon = icon;
            }

            parentItem.Items.Insert(0, item);
        }

        // update parent item
        if (ViewModel.OpenWithTargets.Count > 0)
        {
            // select app
            string? featuredAppId = null;
            switch (ViewModel.CurrentProject?.Format)
            {
                case TargetFormat.PDF:
                case TargetFormat.SinglePagePDF:
                    featuredAppId = ViewModel.SettingsService.LastOpenWithAppPdf;
                    break;
                case TargetFormat.JPG:
                    featuredAppId = ViewModel.SettingsService.LastOpenWithAppJpg;
                    break;
                case TargetFormat.PNG:
                    featuredAppId = ViewModel.SettingsService.LastOpenWithAppPng;
                    break;
                case TargetFormat.BMP:
                    featuredAppId = ViewModel.SettingsService.LastOpenWithAppBmp;
                    break;
                case TargetFormat.TIFF:
                    featuredAppId = ViewModel.SettingsService.LastOpenWithAppTiff;
                    break;
            }

            OpenWithTarget? featuredApp = null;
            if (featuredAppId != null)
                featuredApp = ViewModel.OpenWithTargets.FirstOrDefault(x => x.AppInfo.AppUserModelId == featuredAppId);

            if (featuredApp == null)
                featuredApp = ViewModel.OpenWithTargets[0];

            parentItem.Text = string.Format(GetLocalized(Scanner.Resources.Strings.ResourcesExtension.KeyEnum.OpenWithApp), featuredApp.AppInfo.DisplayInfo.DisplayName);
            parentItem.CommandParameter = featuredApp.AppInfo;

            if (featuredApp.Logo != null)
            {
                featuredApp.Logo!.DecodePixelType = Microsoft.UI.Xaml.Media.Imaging.DecodePixelType.Logical;
                featuredApp.Logo!.DecodePixelWidth = 32;
                featuredApp.Logo!.DecodePixelHeight = 32;

                ImageIcon icon = new ImageIcon
                {
                    Source = featuredApp.Logo
                };

                parentItem.Icon = icon;
            }
        }
    }

    private void ApplyIsMultiSelect()
    {
        OnPropertyChanged(nameof(AreMultiSelectEditActionsAvailable));
        OnPropertyChanged(nameof(IsMultiSelectExportAvailable));
        OnPropertyChanged(nameof(ShowTextBlockTotalPages));

        if (GridViewPageList == null)
            return;

        if (ViewModel.IsMultiSelect)
        {
            GridViewPageList.SelectionMode = ListViewSelectionMode.Multiple;
        }
        else
        {
            GridViewPageList.SelectionMode = ListViewSelectionMode.Single;
        }
    }

    private void ButtonFileNameGeneration_GettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        IsFileNameGenerationButtonFocused = true;
    }

    private void ButtonFileNameGeneration_LostFocus(object sender, RoutedEventArgs e)
    {
        IsFileNameGenerationButtonFocused = false;
    }

    private void ButtonFileNameGeneration_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CopilotRuntimeService.AreModelsInstalled)
        {
            ViewModel.StartStopGenerateFileNameWithAICommand.Execute(null);
        }
        else
        {
            TeachingTipCopilotRuntimeDownload.Target = TextBoxProjectName;
            TeachingTipCopilotRuntimeDownload.IsOpen = true;
        }
    }

    private void ButtonFileNameGeneration_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        TeachingTipAIDisclaimer.Target = TextBoxProjectName;
        TeachingTipAIDisclaimer.IsOpen = true;
    }

    private void FocusProjectNameTextBox()
    {
        TextBoxProjectName.Focus(FocusState.Programmatic);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        KeyboardHookHelper.KeyPressed -= KeyboardHookHelper_KeyPressed;
    }

    private void GridViewItem_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        InitializeGridViewItemAppearance((GridViewItem)sender);
    }

    private void InitializeGridViewItemAppearance(GridViewItem gridViewItem)
    {
        if (gridViewItem.IsSelected)
        {
            Rectangle? innerSelectionIndicator = gridViewItem.FindDescendant("RectangleSelectionIndicatorInner") as Rectangle;
            Rectangle? outerSelectionIndicator = gridViewItem.FindDescendant("RectangleSelectionIndicatorOuter") as Rectangle;
            TextBlock? pageNumber = gridViewItem.FindDescendant("TextBlockPageNumber") as TextBlock;
            if (innerSelectionIndicator != null && outerSelectionIndicator != null && pageNumber != null)
            {
                innerSelectionIndicator.Stroke = Application.Current.Resources["ControlSolidFillColorDefaultBrush"] as Brush;
                innerSelectionIndicator.StrokeThickness = 3;
                outerSelectionIndicator.StrokeThickness = 2;
                pageNumber.Foreground = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as SolidColorBrush;
            }
        }

        if (gridViewItem.Tag != null)
            gridViewItem.UnregisterPropertyChangedCallback(GridViewItem.IsSelectedProperty, (long)gridViewItem.Tag);

        gridViewItem.Tag = gridViewItem.RegisterPropertyChangedCallback(GridViewItem.IsSelectedProperty, OnIsSelectedChanged);
        RoutedEventHandler? handler = null;
        handler = new RoutedEventHandler((s, e) =>
        {
            if (s is GridViewItem item)
            {
                item.UnregisterPropertyChangedCallback(GridViewItem.IsSelectedProperty, (long)gridViewItem.Tag);
                item.Unloaded -= handler;
            }
        });
        gridViewItem.Unloaded += handler;
    }

    private void InvokeShareUI()
    {
        this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            // share UI options currently not supported in WASDK
            //Rect rectangle;
            //ShareUIOptions shareUIOptions = new ShareUIOptions();

            //GeneralTransform transform;
            //transform = GridHeader.TransformToVisual(null);
            //rectangle = transform.TransformBounds(new Rect(0, 0, GridHeader.ActualWidth, GridHeader.ActualHeight));
            //shareUIOptions.SelectionRect = rectangle;

            DataTransferManagerInterop.ShowShareUIForWindow(WindowNative.GetWindowHandle(((App)Application.Current).MainWindow));
        });
    }

    private void GridViewItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (GridViewPageList != null)
            GridViewPageList.SelectedItem = ViewModel.ProjectService.SelectedPage;

        InitializeGridViewItemAppearance((GridViewItem)sender);
    }

    private void GridViewPageList_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        // prevent dragging read-only pages
        if (e.Items.Any(item => item is IProjectPage { IsReadOnly: true }))
            e.Cancel = true;
    }

    private async void GridViewItem_DropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        await ViewModel.ApplyOrderOfPagesToProjectAsyncCommand.ExecuteAsync(null);

        if (GridViewPageList == null)
            return;

        for (int i = 0; i < GridViewPageList.Items.Count; i++)
        {
            GridViewItem? item = GridViewPageList.ContainerFromIndex(i) as GridViewItem;

            if (item != null)
                InitializeGridViewItemAppearance(item);
        }
    }

    private async void GridViewDropZone_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        e.AcceptedOperation = DataPackageOperation.None;
        e.DragUIOverride.Caption = string.Empty;
        e.Handled = true;

        if (!ViewModel.AddDroppedFilesCommand.CanExecute(null))
            return;

        e.DragUIOverride.Caption = Scanner.Resources.Strings.Resources.SettingsScanActionAddToProject;
        e.DragUIOverride.IsCaptionVisible = true;

        DragOperationDeferral deferral = e.GetDeferral();

        var files = await e.DataView.GetStorageItemsAsync();

        try
        {
            if (!files.All(x => AddFilesAction.AcceptedFileExtensions.Contains(System.IO.Path.GetExtension(x.Name))))
            {
                e.DragUIOverride.IsCaptionVisible = false;
                return;
            }

            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void GridViewDropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        if (!ViewModel.AddDroppedFilesCommand.CanExecute(null))
            return;

        e.Handled = true;
        DragOperationDeferral deferral = e.GetDeferral();
        IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
        deferral.Complete();

        List<StorageFile> files = [];
        foreach (IStorageItem item in items)
        {
            if (item is StorageFile file)
                files.Add(file);
        }

        await ViewModel.AddDroppedFilesAsync([.. files]);
    }

    private void GridViewDropZone_Unloaded(object sender, RoutedEventArgs e)
    {
        GridView gridView = (GridView)sender;
        gridView.RemoveHandler(GridView.DragOverEvent, gridViewDragOverHandler);
    }

    private void GridViewCarousel_Loaded(object sender, RoutedEventArgs e)
    {
        GridView gridView = (GridView)sender;
        gridView.AddHandler(GridView.DragOverEvent, gridViewDragOverHandler, true);
    }
}
