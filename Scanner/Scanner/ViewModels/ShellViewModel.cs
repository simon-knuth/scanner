using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Behaviors;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.Windows.Storage.Pickers;
using Scanner.AppWindows;
using Scanner.Extensions;
using Scanner.Helpers;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Resources.Strings;
using Scanner.Services;
using Scanner.Services.Interfaces;
using Sentry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.ViewModels;

partial class ShellViewModel : ObservableRecipient, IDisposable
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetRequiredService<IAccessibilityService>();
    private ILogService? LogService = Ioc.Default.GetService<ILogService>();
    public IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
    private IScannerDiscoveryService ScannerDiscoveryService = Ioc.Default.GetRequiredService<IScannerDiscoveryService>();
    public ISentryService? SentryService = Ioc.Default.GetService<ISentryService>();
    public ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
    #endregion

    #region Events
    public event EventHandler<(TaskCompletionSource<bool> Task, string Trigger)> SaveChangesDialogRequested;
    public event EventHandler<(TaskCompletionSource<SaveOptions?> Process, ScanOptions ScanOptions, ProjectBase? Project, string? DesiredFileDisplayName)> SaveFileDialogRequested;
    public event EventHandler<(TaskCompletionSource<bool> Process, ProjectBase? Project)> ProjectDeletionDialogRequested;
    public event EventHandler<TaskCompletionSource> SaveInProgressDialogRequested;
    public event EventHandler<(string Title, Task Task)> IndeterminateProgressDialogRequested;
    public event EventHandler DonationDialogRequested;
    public event EventHandler OtherAppsDialogRequested;
    public event EventHandler ScanMergeDialogRequested;
    public event EventHandler<ScanOptions> ShowPreviewDialogRequested;
    public event EventHandler<Notification> ShowInAppNotificationRequested;
    public event EventHandler SetupDialogRequested;
    public event EventHandler FeedbackDialogRequested;
    #endregion

    #region Commands
    public RelayCommand ShowSettingsCommand => new RelayCommand(() => ShowSettings());
    public RelayCommand ShowFeedbackCommand => new RelayCommand(ShowFeedback);
    public RelayCommand ShowDonationDialogCommand => new RelayCommand(ShowDonationDialog);
    public RelayCommand ShowOtherAppsDialogCommand => new RelayCommand(ShowOtherAppsDialog);
    public AsyncRelayCommand TryCloseProjectAsyncCommand => new AsyncRelayCommand(TryCloseProjectAsync);
    public AsyncRelayCommand<IProjectAction> TryUndoAsyncCommand => new AsyncRelayCommand<IProjectAction>(TryUndoAsync);
    public AsyncRelayCommand<IProjectAction> TryRedoAsyncCommand => new AsyncRelayCommand<IProjectAction>(TryRedoAsync);
    public AsyncRelayCommand SaveAsyncCommand;
    public AsyncRelayCommand SaveAsAsyncCommand;
    public AsyncRelayCommand SaveAsCurrentPageAsyncCommand;
    public AsyncRelayCommand OpenFilesAsyncCommand => new AsyncRelayCommand(OpenFilesAsync);
    public RelayCommand<DispatcherQueue> ViewLoadingCommand => new RelayCommand<DispatcherQueue>(ViewLoading);
    public RelayCommand DisposeCommand => new RelayCommand(Dispose);
    #endregion

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartNewProject))]
    private ProjectBase? currentProject;

    public bool CanStartNewProject => CurrentProject != null && !ProjectService.IsProcessRunningOrEditing;

    public bool CanUndo => ProjectService.CanUndo && !ProjectService.IsProcessRunningOrEditing;
    public bool CanRedo => ProjectService.CanRedo && !ProjectService.IsProcessRunningOrEditing;

    private TaskCompletionSource viewLoading = new();
    private DispatcherQueue? viewDispatcherQueue;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ShellViewModel()
    {
        SaveAsyncCommand = new AsyncRelayCommand(SaveAsync);
        SaveAsAsyncCommand = new AsyncRelayCommand(SaveAsAsync);
        SaveAsCurrentPageAsyncCommand = new AsyncRelayCommand(SaveAsCurrentPageAsync);

        _ = ScannerDiscoveryService.InitializeSearchAsync();

        ProjectService.PropertyChanged += ProjectService_PropertyChanged;
        ProjectService.ScanCompletedSuccessfully += ProjectService_ScanCompletedSuccessfully;
        CurrentProject = ProjectService.CurrentProject;

        Messenger.Register<ShowUnsavedChangesDialogMessage>(this, (r, m) =>
        {
            m.Reply(ShowSaveChangesDialogAsync("ClosingProject"));
        });
        Messenger.Register<ShowSaveOptionsDialogMessage>(this, (r, m) =>
        {
            m.Reply(ShowSaveFileDialogAsync(m.ScanOptions, m.Project, m.DesiredFileDisplayName));
        });
        Messenger.Register<ShowProjectDeletionDialogMessage>(this, (r, m) =>
        {
            m.Reply(ShowProjectDeletionDialogAsync(m.Project));
        });
        Messenger.Register<ShowSaveInProgressDialogMessage>(this, (r, m) =>
        {
            m.Reply(ShowSaveInProgressDialogAsync());
        });
        Messenger.Register<ShowIndeterminateProgressDialogMessage>(this, (r, m) =>
        {
            m.Reply(ShowIndeterminateProgressDialogAsync(m.Title, m.Process));
        });
        Messenger.Register<ShowDonationDialogMessage>(this, (r, m) =>
        {
            ShowDonationDialog();
        });
        Messenger.Register<ShowInAppNotificationMessage>(this, (r, m) =>
        {
            ShowInAppNotificationRequested?.Invoke(this, m.Notification);
        });
        Messenger.Register<ShowSettingsMessage>(this, (r, m) =>
        {
            ShowSettings(m.Intent);
        });
        Messenger.Register<ShowFeedbackMessage>(this, (r, m) =>
        {
            ShowFeedback();
        });
        Messenger.Register<ShowScanMergeDialogMessage>(this, (r, m) =>
        {
            ShowScanMergeDialog();
        });
        Messenger.Register<ShowPreviewDialogMessage>(this, (r, m) =>
        {
            ShowPreviewDialog(m.ScanOptions);
        });
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Dispose()
    {
        Messenger.UnregisterAll(this);
    }

    private void ViewLoading(DispatcherQueue? dispatcherQueue)
    {
        viewDispatcherQueue = dispatcherQueue;
        ProjectService.UiDispatcherQueue = dispatcherQueue;
        viewLoading.TrySetResult();

        ((App)Application.Current).MainWindow.Closed += MainWindow_Closed;

        if (!SettingsService.SetupCompleted && SentryService != null)
            ShowSetupDialog();
    }

    private void ProjectService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IProjectService.CurrentProject):
                CurrentProject = ProjectService.CurrentProject;
                break;
            case nameof(IProjectService.IsProcessRunningOrEditing):
                OnPropertyChanged(nameof(CanStartNewProject));
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
                break;
            case nameof(IProjectService.CanUndo):
                OnPropertyChanged(nameof(CanUndo));
                break;
            case nameof(IProjectService.CanRedo):
                OnPropertyChanged(nameof(CanRedo));
                break;
        }
    }

    private async Task<bool> ShowSaveChangesDialogAsync(string trigger)
    {
        TaskCompletionSource<bool> result = new();
        SaveChangesDialogRequested?.Invoke(this, (result, trigger));
        return await result.Task;
    }

    private async Task<SaveOptions?> ShowSaveFileDialogAsync(ScanOptions scanOptions, ProjectBase? project, string? desiredFileDisplayName)
    {
        TaskCompletionSource<SaveOptions?> result = new();
        SaveFileDialogRequested?.Invoke(this, new(result, scanOptions, project, desiredFileDisplayName));
        return await result.Task;
    }

    private async Task ShowSaveInProgressDialogAsync()
    {
        TaskCompletionSource result = new();
        SaveInProgressDialogRequested?.Invoke(this, result);
        await result.Task;
    }

    private async Task<bool> ShowProjectDeletionDialogAsync(ProjectBase project)
    {
        TaskCompletionSource<bool> result = new();
        ProjectDeletionDialogRequested?.Invoke(this, (result, project));
        return await result.Task;
    }

    private async Task ShowIndeterminateProgressDialogAsync(string title, Task process)
    {
        if (!ProjectService.IsScanProcessRunning)
            IndeterminateProgressDialogRequested?.Invoke(this, (title, process));
        
        await process;
    }

    private async void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (CurrentProject != null && !CurrentProject.IsSaved)
        {
            args.Handled = true;

            // unsaved changes present
            if (SettingsService.SettingAutoSave)
            {
                // inform user
                await ShowSaveInProgressDialogAsync();

                // process result
                if (CurrentProject.IsSaved)
                {
                    // changes saved successfully ~> close window for good
                    Messenger.Send(new MainWindowClosingMessage((MainWindow)sender));
                    ((MainWindow)sender).Closed -= MainWindow_Closed;
                    ((MainWindow)sender).Close();
                }
                else
                {
                    return;
                }
            }
            else
            {
                // ask user
                bool result = await ShowSaveChangesDialogAsync("ClosingWindow");

                // process result
                if (result)
                {
                    // changes saved or discarded ~> close window for good
                    Messenger.Send(new MainWindowClosingMessage((MainWindow)sender));
                    ((MainWindow)sender).Closed -= MainWindow_Closed;
                    ((MainWindow)sender).Close();
                }
                else
                { 
                    return;
                }
            }
        }
        Messenger.Send(new MainWindowClosingMessage((MainWindow)sender));
    }

    private async Task SaveAsync()
    {
        if (CurrentProject == null) return;
        await CurrentProject.SaveAsync(false, viewDispatcherQueue!, isUserInitiated: true);
    }

    private async Task SaveAsAsync()
    {
        if (CurrentProject == null) return;
        await CurrentProject.SaveAsync(true, viewDispatcherQueue!, isUserInitiated: true);
    }

    private async Task SaveAsCurrentPageAsync()
    {
        if (CurrentProject == null) return;
        if (CurrentProject is not MultiFileProject imageProject) return;

        if (ProjectService.SelectedPage != null)
            await imageProject.SaveAsSinglePageAsync(ProjectService.SelectedPage, viewDispatcherQueue!);
    }

    private async Task TryCloseProjectAsync()
    {
        await ProjectService.TryCloseProjectAsync();
    }

    private async Task TryUndoAsync(IProjectAction? upUntil = null)
    {
        await ProjectService.TryUndoAsync(upUntil);
    }

    private async Task TryRedoAsync(IProjectAction? upUntil = null)
    {
        await ProjectService.TryRedoAsync(upUntil);
    }

    private void ShowSettings(SettingsViewModelIntent? intent = null)
    {
        SentryService?.TrackEvent(AnalyticsEvent.SettingsRequested, new Dictionary<string, string>
        {
            { "section", (intent?.DisplayedPage ?? SettingsPageType.General).ToString() }
        });
        ((App)Application.Current).ShowSettings(intent);
    }

    private void ShowFeedback()
    {
        ShowSettings(new SettingsViewModelIntent(SettingsPageType.Feedback));
    }

    private void ShowDonationDialog()
    {
        SentryService?.TrackEvent(AnalyticsEvent.DonationDialogOpened);
        DonationDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowOtherAppsDialog()
    {
        SentryService?.TrackEvent(AnalyticsEvent.OtherAppsDialogOpened);
        OtherAppsDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowScanMergeDialog()
    {
        ScanMergeDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowPreviewDialog(ScanOptions scanOptions)
    {
        ShowPreviewDialogRequested?.Invoke(this, scanOptions);
    }

    private void ShowSetupDialog()
    {
        SentryService?.TrackEvent(AnalyticsEvent.SetupStarted);
        SetupDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task OpenFilesAsync()
    {
        IReadOnlyList<PickFileResult> pickerResults = await Helpers.Helpers.PickInputFilesAsync(true, viewDispatcherQueue!);
        if (pickerResults.Count == 0)
            return;

        SentryService?.TrackEvent(AnalyticsEvent.ProjectOpenedFromDisk, new Dictionary<string, string>
        {
            { "files", pickerResults.Count.ToString() }
        });

        await ProjectService.TryOpenProjectFromFilesAsync(pickerResults.Select(x => x.Path).ToArray(), null, viewDispatcherQueue!);
    }

    private void ShowFeedbackDialog()
    {
        FeedbackDialogRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ProjectService_ScanCompletedSuccessfully(object? sender, EventArgs e)
    {
        if (SettingsService.ScanNumber == AppConfig.ScanNumberToTriggerFeedbackDialog)
            ShowFeedbackDialog();
    }
}