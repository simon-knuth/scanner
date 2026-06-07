using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Scanner.Extensions;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.ViewModels;

class HistoryViewModel : ObservableRecipient, IDisposable
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    public readonly IProjectHistoryService ProjectHistoryService = Ioc.Default.GetRequiredService<IProjectHistoryService>();
    public readonly IProjectService ProjectService = Ioc.Default.GetRequiredService<IProjectService>();
    private readonly ISentryService? SentryService = Ioc.Default.GetService<ISentryService>();
    #endregion

    #region Commands
    public AsyncRelayCommand<ProjectHistoryEntry> OpenEntryAsyncCommand;
    public AsyncRelayCommand<ProjectHistoryEntry> RemoveEntryAsyncCommand;
    public AsyncRelayCommand ClearListAsyncCommand;
    public AsyncRelayCommand<ProjectHistoryEntry> ShowInFileExplorerAsyncCommand;
    public RelayCommand DisposeCommand => new RelayCommand(Dispose);
    #endregion

    private DispatcherQueue viewDispatcherQueue;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public HistoryViewModel()
    {
        OpenEntryAsyncCommand = new(OpenEntryAsync);
        RemoveEntryAsyncCommand = new(RemoveEntryAsync);
        ClearListAsyncCommand = new(ClearListAsync);
        ShowInFileExplorerAsyncCommand = new(ShowInFileExplorerAsync);

        Task.Run(ProjectHistoryService.UpdateMissingFilesAsync);

        SentryService?.TrackEvent(AnalyticsEvent.HistoryViewOpened);
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Dispose()
    {
        Messenger.UnregisterAll(this);
    }

    public void ViewLoaded(DispatcherQueue dispatcherQueue)
    {
        viewDispatcherQueue = dispatcherQueue;
    }

    public async Task OpenEntryAsync(ProjectHistoryEntry entry)
    {
        SentryService?.TrackEvent(AnalyticsEvent.HistoryEntryOpened);
        await ProjectService.TryOpenProjectFromFilesAsync(entry.Files.Select(x => x.FilePath).ToArray(), entry.Id, viewDispatcherQueue);
    }

    public async Task RemoveEntryAsync(ProjectHistoryEntry entry)
    {
        await ProjectHistoryService.RemoveEntryAsync(entry.Id);
        SentryService?.TrackEvent(AnalyticsEvent.HistoryEntryRemoved);
    }

    public async Task ClearListAsync()
    {
        await ProjectHistoryService.ClearHistoryAsync();
        SentryService?.TrackEvent(AnalyticsEvent.HistoryCleared);
    }

    private async Task ShowInFileExplorerAsync(ProjectHistoryEntry entry)
    {
        SentryService?.TrackEvent(AnalyticsEvent.HistoryEntryShownInFileExplorer);
        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(entry.Files[0].FilePath));
        await Windows.System.Launcher.LaunchFolderAsync(folder);
    }
}
