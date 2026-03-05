using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Behaviors;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Services.Interfaces;
using Scanner.Views.Dialogs;
using Scanner.Views.Flyouts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Cryptography.Certificates;
using Windows.Storage;


namespace Scanner.Views;

[ObservableObjectAttribute]
public sealed partial class ShellView : Page
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Constants
    private const double ScanOptionsPaneMaxWidth = 348;
    private const double ProjectViewPaneMaxWidth = 452;
    #endregion

    private double LeftPaneMaxWidth = ScanOptionsPaneMaxWidth;
    private double RightPaneMaxWidth = ProjectViewPaneMaxWidth;
    
    private FlowDirection LayoutFlowDirection => ViewModel.SettingsService.SettingMirrorAppLayout ?
        ViewModel.AccessibilityService.InvertedFlowDirection : ViewModel.AccessibilityService.DefaultFlowDirection;

    public bool ShowExpandButtonInProjectView => VisualStateGroup.CurrentState == VisualStateNarrow && !ProjectView.IsExpanded;
    public bool ShowAllMoreMenuItems => VisualStateGroup.CurrentState == VisualStateNarrow || VisualStateGroup.CurrentState == VisualStateNarrowNoProject;

    public bool ShowScanActionsDivider => ScanActionsView.AreScanOptionsVisible || ScanOptionsView.CanScroll || ViewModel.ProjectService.IsScanProcessRunning;

    public string SaveButtonGlyph => ViewModel.CurrentProject == null || ViewModel.CurrentProject.IsPdf ? "\uE74E" : "\uEA35";

    private bool isDialogVisible;

    private Notification DebugNotification = new Notification
    {
        Title = "This is a test notification",
        Message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed non risus. Suspendisse lectus tortor, dignissim sit amet, adipiscing nec, ultricies sed, dolor.",
        Severity = InfoBarSeverity.Informational
    };

    private int lastPageCount = 0;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ShellView()
    {
        this.InitializeComponent();

        if (ViewModel.SettingsService.SettingMirrorAppLayout)
        {
            LeftPaneMaxWidth = ProjectViewPaneMaxWidth;
            RightPaneMaxWidth = ProjectViewPaneMaxWidth;
        }

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.SaveChangesDialogRequested += ViewModel_SaveChangesDialogRequested;
        ViewModel.SaveFileDialogRequested += ViewModel_SaveFileDialogRequested;
        ViewModel.SaveInProgressDialogRequested += ViewModel_SaveInProgressDialogRequested;
        ViewModel.ProjectDeletionDialogRequested += ViewModel_ProjectDeletionDialogRequested;
        ViewModel.IndeterminateProgressDialogRequested += ViewModel_IndeterminateProgressDialogRequested;
        ViewModel.DonationDialogRequested += ViewModel_DonationDialogRequested;
        ViewModel.OtherAppsDialogRequested += ViewModel_OtherAppsDialogRequested;
        ViewModel.ScanMergeDialogRequested += ViewModel_ScanMergeDialogRequested;
        ViewModel.ShowInAppNotificationRequested += ViewModel_ShowInAppNotificationRequested;
        ViewModel.ProjectService.PropertyChanged += ProjectService_PropertyChanged;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ViewModel.CurrentProject):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    UpdateVisualState(GridRoot.ActualWidth);
                    if (ViewModel.CurrentProject != null)
                    {
                        ViewModel.CurrentProject.PagesAdded += CurrentProject_PagesAdded;

                        if (lastPageCount < 2 && ViewModel.CurrentProject.Pages.Count >= 2 && ViewModel.SettingsService.SettingExpandPageList)
                        {
                            TryExpandPageList();
                        }
                        lastPageCount = ViewModel.CurrentProject.Pages.Count;
                    }
                    else
                    {
                        lastPageCount = 0;
                    }

                    OnPropertyChanged(nameof(SaveButtonGlyph));
                });
                break;
        }
    }

    private void CurrentProject_PagesAdded(object? sender, EventArgs e)
    {
        if (sender == null) return;

        if (lastPageCount < 2 && ((ProjectBase)sender).Pages.Count >= 2 && ViewModel.SettingsService.SettingExpandPageList)
        {
            this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, TryExpandPageList);
        }
        lastPageCount = ((ProjectBase)sender).Pages.Count;
    }

    private void UpdateVisualState(double width)
    {
        if (width < 700)
        {
            // narrow
            if (ViewModel.CurrentProject == null)
            {
                if (VisualStateGroup.CurrentState == VisualStateNarrowNoProject)
                    return;

                VisualStateManager.GoToState(this, nameof(VisualStateNarrowNoProject), false);
            }
            else
            {
                if (VisualStateGroup.CurrentState == VisualStateNarrow)
                    return;

                VisualStateManager.GoToState(this, nameof(VisualStateNarrow), false);
            }

            // show UI based on current selection
            if (ViewModel.ProjectService.SelectedPagesCount == 0)
            {
                // 0 pages selected ~> scan options
                BorderEditor.Visibility = Visibility.Collapsed;
                BorderScanOptions.Visibility = Visibility.Visible;
                ProjectView.IsExpanded = false;
                ScanActionsView.AreScanOptionsVisible = false;
            }
            else if (ViewModel.ProjectService.SelectedPagesCount == 1)
            {
                // 1 page selected ~> editor
                ProjectView.IsExpanded = false;
                ScanActionsView.AreScanOptionsVisible = true;
                BorderScanOptions.Visibility = Visibility.Collapsed;
                BorderEditor.Visibility = Visibility.Visible;
            }
            else
            {
                // multiple pages selected ~> page list
                BorderEditor.Visibility = Visibility.Collapsed;
                BorderScanOptions.Visibility = Visibility.Collapsed;
                TryExpandPageList();
                ScanActionsView.AreScanOptionsVisible = true;
            }
            
        }
        else if (width < 1500)
        {
            // default
            if (ViewModel.CurrentProject == null)
            {
                if (VisualStateGroup.CurrentState == VisualStateDefaultNoProject)
                    return;

                VisualStateManager.GoToState(this, nameof(VisualStateDefaultNoProject), false);
            }
            else
            {
                if (VisualStateGroup.CurrentState == VisualStateDefault)
                    return;

                VisualStateManager.GoToState(this, nameof(VisualStateDefault), false);
            }

            // ensure selection
            if (ViewModel.CurrentProject != null && ViewModel.ProjectService.SelectedPagesCount == 0)
                ViewModel.ProjectService.MakeDefaultSelection();

            // show scan options based on page list
            if (!ProjectView.IsExpanded)
                BorderScanOptions.Visibility = Visibility.Visible;

            BorderEditor.Visibility = Visibility.Visible;
        }
        else
        {
            // wide
            if (VisualStateGroup.CurrentState == VisualStateWide)
                return;

            VisualStateManager.GoToState(this, nameof(VisualStateWide), false);

            // ensure selection
            if (ViewModel.CurrentProject != null && ViewModel.ProjectService.SelectedPagesCount == 0)
                ViewModel.ProjectService.MakeDefaultSelection();

            // show scan options based on page list
            if (!ProjectView.IsExpanded)
                BorderScanOptions.Visibility = Visibility.Visible;

            BorderEditor.Visibility = Visibility.Visible;
        }
    }

    private void SetRegionsForCustomTitleBar()
    {
        AppWindowTitleBar titlebar = ((App)Application.Current).MainWindow.AppWindow.TitleBar;

        double scaleAdjustment = this.XamlRoot.RasterizationScale;
        double headerInset = ViewModel.AccessibilityService.DefaultFlowDirection == FlowDirection.LeftToRight ? titlebar.LeftInset + 0 : titlebar.RightInset + 24;
        double footerInset = ViewModel.AccessibilityService.DefaultFlowDirection == FlowDirection.LeftToRight ? titlebar.RightInset + 24 : titlebar.LeftInset + 0;
        ColumnDefinitionTitlebarInsetHeader.Width = new GridLength(headerInset / scaleAdjustment);
        ColumnDefinitionTitlebarInsetFooter.Width = new GridLength(footerInset / scaleAdjustment);

        GeneralTransform transform = StackPanelTitlebarButtonsLeft.TransformToVisual(null);
        Rect bounds = transform.TransformBounds(new Rect(0, 0,
                                                         StackPanelTitlebarButtonsLeft.ActualWidth,
                                                         StackPanelTitlebarButtonsLeft.ActualHeight));
        Windows.Graphics.RectInt32 SearchBoxRect = GetRect(bounds, scaleAdjustment);

        transform = StackPanelTitlebarButtonsRight.TransformToVisual(null);
        bounds = transform.TransformBounds(new Rect(0, 0,
                                                    StackPanelTitlebarButtonsRight.ActualWidth,
                                                    StackPanelTitlebarButtonsRight.ActualHeight));
        Windows.Graphics.RectInt32 PersonPicRect = GetRect(bounds, scaleAdjustment);

        var rectArray = new Windows.Graphics.RectInt32[] { SearchBoxRect, PersonPicRect };

        InputNonClientPointerSource nonClientInputSrc =
            InputNonClientPointerSource.GetForWindowId(((App)Application.Current).MainWindow.AppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);
    }

    private Windows.Graphics.RectInt32 GetRect(Rect bounds, double scale)
    {
        return new Windows.Graphics.RectInt32(
            _X: (int)Math.Round(bounds.X * scale),
            _Y: (int)Math.Round(bounds.Y * scale),
            _Width: (int)Math.Round(bounds.Width * scale),
            _Height: (int)Math.Round(bounds.Height * scale)
        );
    }

    private void GridRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateVisualState(e.NewSize.Width);
        SetRegionsForCustomTitleBar();
    }

    private void GridRoot_Loaded(object sender, RoutedEventArgs e)
    {
        SetRegionsForCustomTitleBar();
    }

    private void ExpandPageListRequested(object sender, EventArgs e)
    {
        TryExpandPageList();
    }

    private void TryExpandPageList()
    {
        if (ProjectView.IsExpanded) return;

        if (VisualStateGroup.CurrentState == VisualStateNarrow
            || VisualStateGroup.CurrentState == VisualStateNarrowNoProject)
        {
            BorderEditor.Visibility = Visibility.Collapsed;
            ViewModel.ProjectService.SelectedPage = null;
            ViewModel.ProjectService.SelectedPages = null;
        }

        ProjectView.IsExpanded = true;
        BorderScanOptions.Visibility = Visibility.Collapsed;
        ScanActionsView.AreScanOptionsVisible = true;
    }

    private void ScanActionsView_ExpandScanOptionsRequested(object sender, EventArgs e)
    {
        if (VisualStateGroup.CurrentState == VisualStateNarrow
            || VisualStateGroup.CurrentState == VisualStateNarrowNoProject)
        {
            BorderEditor.Visibility = Visibility.Collapsed;
            ViewModel.ProjectService.SelectedPage = null;
            ViewModel.ProjectService.SelectedPages = null;
        }

        ScanActionsView.AreScanOptionsVisible = false;
        BorderScanOptions.Visibility = Visibility.Visible;
        ProjectView.IsExpanded = false;
    }

    private void MenuFlyoutItemHistory_Click(object sender, RoutedEventArgs e)
    {
        ShowHistory(ButtonTitlebarMore);
    }

    private void ButtonHistory_Click(object sender, RoutedEventArgs e)
    {
        ShowHistory(ButtonHistory);
    }

    private void ShowHistory(FrameworkElement target)
    {
        Flyout flyout = new Flyout();
        HistoryView historyView = new HistoryView
        {
            Margin = new Thickness(-16),
            Width = 348,
            Height = 400,
        };
        EventHandler closeRequestedHandler = new((sender, args) => flyout.Hide());
        historyView.CloseRequested += closeRequestedHandler;
        flyout.Content = historyView;
        flyout.FlyoutPresenterStyle = (Style)Resources["NoScrollFlyoutPresenterStyle"];
        flyout.ShowAt(target);
        EventHandler<object>? closedHandler = null;
        closedHandler = new((sender, args) =>
        {
            flyout.Closed -= closedHandler;
            historyView.CloseRequested -= closeRequestedHandler;
        });
        flyout.Closed += closedHandler;
    }

    private void VisualStateGroup_CurrentStateChanged(object sender, VisualStateChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ShowExpandButtonInProjectView));
        OnPropertyChanged(nameof(ShowAllMoreMenuItems));
    }

    private void ProjectView_IsExpandedChanged(object sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ShowExpandButtonInProjectView));
        OnPropertyChanged(nameof(ShowScanActionsDivider));
    }

    private void ViewModel_SaveChangesDialogRequested(object? sender, TaskCompletionSource<bool> e)
    {
        ShowSaveChangesDialog(e);
    }

    private void ViewModel_SaveFileDialogRequested(object? sender, (TaskCompletionSource<SaveOptions?> Process, ScanOptions ScanOptions, ProjectBase? Project, string? DesiredFileDisplayName) e)
    {
        ShowSaveFileDialog(e.Process, e.ScanOptions, e.Project, e.DesiredFileDisplayName);
    }

    private void ViewModel_SaveInProgressDialogRequested(object? sender, TaskCompletionSource e)
    {
        ShowSaveInProgressDialog(e);
    }

    private void ViewModel_ProjectDeletionDialogRequested(object? sender, (TaskCompletionSource<bool> Process, ProjectBase? Project) e)
    {
        ShowProjectDeletionDialog(e.Process, e.Project);
    }

    private void ViewModel_IndeterminateProgressDialogRequested(object? sender, (string Title, Task Task) e)
    {
        ShowIndeterminateProgressDialog(e.Title, e.Task);
    }

    private void ViewModel_DonationDialogRequested(object? sender, EventArgs e)
    {
        ShowDonationDialog();
    }

    private void ViewModel_OtherAppsDialogRequested(object? sender, EventArgs e)
    {
        ShowOtherAppsDialog();
    }

    private void ViewModel_ScanMergeDialogRequested(object? sender, EventArgs e)
    {
        ShowScanMergeDialog();
    }

    private void SettingsCardDebugDialogSaveChanges_Click(object sender, RoutedEventArgs e)
    {
        ShowSaveChangesDialog(new TaskCompletionSource<bool>());
    }

    private void ShowSaveChangesDialog(TaskCompletionSource<bool> task)
    {
        this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
        {
            // return if dialog is already visible
            if (isDialogVisible)
            {
                task.TrySetResult(false);
                return;
            }

            isDialogVisible = true;

            UnsavedChangesDialogView dialog = new UnsavedChangesDialogView(ViewModel.CurrentProject);
            dialog.XamlRoot = this.XamlRoot;
            ContentDialogResult result = await dialog.ShowAsync();

            isDialogVisible = false;
            task.TrySetResult(result != ContentDialogResult.None);
        });
    }

    private void ShowSaveFileDialog(TaskCompletionSource<SaveOptions?> task, ScanOptions scanOptions, ProjectBase? project, string? desiredFileDisplayName)
    {
        this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
        {
            // return if dialog is already visible
            if (isDialogVisible)
            {
                task.TrySetResult(null);
                return;
            }

            isDialogVisible = true;

            SaveOptionsDialogView dialog = new SaveOptionsDialogView(scanOptions, project, desiredFileDisplayName);
            dialog.XamlRoot = this.XamlRoot;
            ContentDialogResult result = await dialog.ShowAsync();

            isDialogVisible = false;
            if (result == ContentDialogResult.Primary) task.TrySetResult(dialog.SaveOptions);
            else task.TrySetResult(null);
        });
    }

    private void ShowSaveInProgressDialog(TaskCompletionSource task)
    {
        this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
        {
            // return if dialog is already visible
            if (isDialogVisible)
            {
                task.TrySetResult();
                return;
            }

            isDialogVisible = true;

            SaveInProgressDialogView dialog = new SaveInProgressDialogView(ViewModel.CurrentProject);
            dialog.XamlRoot = this.XamlRoot;
            ContentDialogResult result = await dialog.ShowAsync();

            isDialogVisible = false;
            task.TrySetResult();
        });
    }

    private void ShowProjectDeletionDialog(TaskCompletionSource<bool> task, ProjectBase project)
    {
        this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
        {
            // return if dialog is already visible
            if (isDialogVisible)
            {
                task.TrySetResult(false);
                return;
            }

            isDialogVisible = true;

            ProjectDeletionDialogView dialog = new ProjectDeletionDialogView(project);
            dialog.XamlRoot = this.XamlRoot;
            ContentDialogResult result = await dialog.ShowAsync();

            isDialogVisible = false;
            if (result == ContentDialogResult.Primary) task.TrySetResult(true);
            else task.TrySetResult(false);
        });
    }

    private void ShowIndeterminateProgressDialog(string title, Task task)
    {
        this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
        {
            // return if dialog is already visible
            if (isDialogVisible)
            {
                return;
            }

            isDialogVisible = true;

            IndeterminateProgressDialogView dialog = new(title, task);
            dialog.XamlRoot = this.XamlRoot;
            ContentDialogResult result = await dialog.ShowAsync();

            isDialogVisible = false;
        });
    }

    private void ShowDonationDialog()
    {
        this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
        {
            // return if dialog is already visible
            if (isDialogVisible)
            {
                return;
            }

            isDialogVisible = true;

            DonationDialogView dialog = new DonationDialogView();
            dialog.XamlRoot = this.XamlRoot;
            ContentDialogResult result = await dialog.ShowAsync();

            isDialogVisible = false;
        });
    }

    private void ShowOtherAppsDialog()
    {
        this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
        {
            // return if dialog is already visible
            if (isDialogVisible)
            {
                return;
            }

            isDialogVisible = true;

            OtherAppsDialogView dialog = new OtherAppsDialogView();
            dialog.XamlRoot = this.XamlRoot;
            ContentDialogResult result = await dialog.ShowAsync();

            isDialogVisible = false;
        });
    }

    private void ShowScanMergeDialog()
    {
        this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
        {
            // return if dialog is already visible
            if (isDialogVisible)
            {
                return;
            }

            isDialogVisible = true;

            ScanMergeDialogView dialog = new ScanMergeDialogView();
            dialog.XamlRoot = this.XamlRoot;
            ContentDialogResult result = await dialog.ShowAsync();

            isDialogVisible = false;
        });
    }

    private void ViewModel_ShowInAppNotificationRequested(object? sender, Notification e)
    {
        this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            NotificationQueue.Show(e);
        });
    }

    private void ButtonDebugInAppNotification_Click(object sender, RoutedEventArgs e)
    {
        DebugNotification.Severity = (InfoBarSeverity)ComboBoxDebugInAppNotificationsSeverity.SelectedIndex;
        NotificationQueue.Show(new Notification
        {
            Title = DebugNotification.Title,
            Message = DebugNotification.Message,
            Severity = DebugNotification.Severity
        });
    }

    private void ScanActionsView_AreScanOptionsVisibleChanged(object sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ShowScanActionsDivider));
    }

    private void ScanOptionsView_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ScanOptionsView.CanScroll):
                OnPropertyChanged(nameof(ShowScanActionsDivider));
                break;
        }
    }

    private void ProjectService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IProjectService.IsScanProcessRunning):
                this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    OnPropertyChanged(nameof(ShowScanActionsDivider));
                });
                break;
            case nameof(IProjectService.SelectedPage):
            case nameof(IProjectService.SelectedPages):
                // show editor if narrow layout and no multi-select
                if (ViewModel.ProjectService.SelectedPages != null || ViewModel.ProjectService.SelectedPage == null)
                    return;

                if (VisualStateGroup.CurrentState == VisualStateNarrow
                    || VisualStateGroup.CurrentState == VisualStateNarrowNoProject)
                {
                    ProjectView.IsExpanded = false;
                    ScanActionsView.AreScanOptionsVisible = true;
                    BorderScanOptions.Visibility = Visibility.Collapsed;
                    BorderEditor.Visibility = Visibility.Visible;
                }
                break;
        }
    }

    private void ButtonUndo_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        UndoRedoStackFlyout flyout = new UndoRedoStackFlyout(ViewModel.ProjectService.UndoStack);
        flyout.ActionSelected += async (sender, e) => await ViewModel.TryUndoAsyncCommand.ExecuteAsync(e);
        flyout.ShowAt((FrameworkElement)sender);
    }

    private void ButtonRedo_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        UndoRedoStackFlyout flyout = new UndoRedoStackFlyout(ViewModel.ProjectService.RedoStack);
        flyout.ActionSelected += async (sender, e) => await ViewModel.TryRedoAsyncCommand.ExecuteAsync(e);
        flyout.ShowAt((FrameworkElement)sender);
    }

    private void ButtonSave_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout(ButtonSave);
    }

    private async void Page_Loading(FrameworkElement sender, object args)
    {
        await ViewModel.AccessibilityService.InitializeForLanguageTagAsync(this.DispatcherQueue, sender.Language);
    }

    private void ButtonTitlebarMore_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
#if DEBUG
        FlyoutBase.ShowAttachedFlyout(ButtonTitlebarMore);
#endif
    }
}
