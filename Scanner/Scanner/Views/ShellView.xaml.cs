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


namespace Scanner.Views
{
    [ObservableObjectAttribute]
    public sealed partial class ShellView : Page
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private FlowDirection LayoutFlowDirection => ViewModel.SettingsService.SettingMirrorAppLayout ?
            ViewModel.AccessibilityService.InvertedFlowDirection : ViewModel.AccessibilityService.DefaultFlowDirection;

        public bool ShowExpandButtonInProjectView => VisualStateGroup.CurrentState == VisualStateNarrow && !ProjectView.IsExpanded;

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

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.SaveChangesDialogRequested += ViewModel_SaveChangesDialogRequested;
            ViewModel.SaveFileDialogRequested += ViewModel_SaveFileDialogRequested;
            ViewModel.SaveInProgressDialogRequested += ViewModel_SaveInProgressDialogRequested;
            ViewModel.ProjectDeletionDialogRequested += ViewModel_ProjectDeletionDialogRequested;
            ViewModel.MultiEditInProgressDialogRequested += ViewModel_MultiEditInProgressDialogRequested;
            ViewModel.ShowNotificationRequested += ViewModel_ShowNotificationRequested;
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
                    VisualStateManager.GoToState(this, nameof(VisualStateNarrowNoProject), false);
                }
                else
                {
                    VisualStateManager.GoToState(this, nameof(VisualStateNarrow), false);
                }
            }
            else if (width < 1500)
            {
                // default
                if (ViewModel.CurrentProject == null)
                {
                    VisualStateManager.GoToState(this, nameof(VisualStateDefaultNoProject), false);
                }
                else
                {
                    VisualStateManager.GoToState(this, nameof(VisualStateDefault), false);
                }
            }
            else
            {
                // wide
                VisualStateManager.GoToState(this, nameof(VisualStateWide), false);
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

            ProjectView.IsExpanded = true;
            BorderScanOptions.Visibility = Visibility.Collapsed;
            ScanActionsView.AreScanOptionsVisible = true;
        }

        private void ScanActionsView_ExpandScanOptionsRequested(object sender, EventArgs e)
        {
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
            flyout.Content = new HistoryView
            {
                Margin = new Thickness(-16),
                MinWidth = 348,
                MinHeight = 400
            };
            flyout.ShowAt(target);
        }

        private void VisualStateGroup_CurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ShowExpandButtonInProjectView));
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

        private void ViewModel_SaveFileDialogRequested(object? sender, (TaskCompletionSource<SaveOptions?> Process, ScanOptions ScanOptions, ProjectBase? Project) e)
        {
            ShowSaveFileDialog(e.Process, e.ScanOptions, e.Project);
        }

        private void ViewModel_SaveInProgressDialogRequested(object? sender, TaskCompletionSource e)
        {
            ShowSaveInProgressDialog(e);
        }

        private void ViewModel_ProjectDeletionDialogRequested(object? sender, (TaskCompletionSource<bool> Process, ProjectBase? Project) e)
        {
            ShowProjectDeletionDialog(e.Process, e.Project);
        }

        private void ViewModel_MultiEditInProgressDialogRequested(object? sender, Task e)
        {
            ShowMultiEditInProgressDialog(e);
        }

        private void ButtonSettings_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
#if DEBUG
            FlyoutBase.ShowAttachedFlyout(ButtonSettings);
#endif
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
                task.TrySetResult(result != ContentDialogResult.None);

                isDialogVisible = false;
            });
        }

        private void ShowSaveFileDialog(TaskCompletionSource<SaveOptions?> task, ScanOptions scanOptions, ProjectBase? project)
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

                SaveOptionsDialogView dialog = new SaveOptionsDialogView(scanOptions, project);
                dialog.XamlRoot = this.XamlRoot;
                ContentDialogResult result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary) task.TrySetResult(dialog.SaveOptions);
                else task.TrySetResult(null);

                isDialogVisible = false;
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
                task.TrySetResult();

                isDialogVisible = false;
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
                if (result == ContentDialogResult.Primary) task.TrySetResult(true);
                else task.TrySetResult(false);

                isDialogVisible = false;
            });
        }

        private void ShowMultiEditInProgressDialog(Task task)
        {
            this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                // return if dialog is already visible
                if (isDialogVisible)
                {
                    return;
                }

                isDialogVisible = true;

                MultiEditInProgressDialogView dialog = new MultiEditInProgressDialogView(task);
                dialog.XamlRoot = this.XamlRoot;
                ContentDialogResult result = await dialog.ShowAsync();

                isDialogVisible = false;
            });
        }

        private void ViewModel_ShowNotificationRequested(object? sender, CommunityToolkit.WinUI.Behaviors.Notification e)
        {
            this.RunOnUIThread(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                NotificationQueue.Show(e);
            });
        }

        private void ButtonDebugNotification_Click(object sender, RoutedEventArgs e)
        {
            DebugNotification.Severity = (InfoBarSeverity)ComboBoxDebugNotificationsSeverity.SelectedIndex;
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

        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            ((App)Application.Current).InvokeKeyDown(e);
        }

        private async void Page_Loading(FrameworkElement sender, object args)
        {
            await ViewModel.AccessibilityService.InitializeForLanguageTagAsync(this.DispatcherQueue, sender.Language);
        }
    }
}
