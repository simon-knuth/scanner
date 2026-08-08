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
using System.Collections.Specialized;
using System.Globalization;
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
    public AsyncRelayCommand<ProjectHistoryEntry> RemoveEntryAsyncCommand;
    public AsyncRelayCommand ClearListAsyncCommand;
    public AsyncRelayCommand<ProjectHistoryEntry> ShowInFileExplorerAsyncCommand;
    public RelayCommand DisposeCommand => new RelayCommand(Dispose);
    #endregion

    /// <summary>
    ///     Entries grouped by day.
    /// </summary>
    public ObservableCollection<ProjectHistoryEntryGroup> GroupedEntries { get; } = [];

    private DispatcherQueue viewDispatcherQueue;
    private bool groupRebuildPending;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public HistoryViewModel()
    {
        RemoveEntryAsyncCommand = new(RemoveEntryAsync);
        ClearListAsyncCommand = new(ClearListAsync);
        ShowInFileExplorerAsyncCommand = new(ShowInFileExplorerAsync);

        ProjectHistoryService.Entries.CollectionChanged += Entries_CollectionChanged;
        RebuildGroups();

        Task.Run(ProjectHistoryService.UpdateMissingFilesAsync);

        SentryService?.TrackEvent(AnalyticsEvent.HistoryViewOpened);
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Dispose()
    {
        ProjectHistoryService.Entries.CollectionChanged -= Entries_CollectionChanged;
        Messenger.UnregisterAll(this);
    }

    private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (groupRebuildPending)
            return;

        groupRebuildPending = true;

        // CollectionChanged is raised on the UI thread; defer to the end of the batch.
        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue is null)
        {
            groupRebuildPending = false;
            RebuildGroups();
            return;
        }

        dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            groupRebuildPending = false;
            RebuildGroups();
        });
    }

    private void RebuildGroups()
    {
        // entries already sorted newest-first, preserved in groups
        List<(DateTime Date, List<ProjectHistoryEntry> Entries)> desired = ProjectHistoryService.Entries
            .GroupBy(entry => entry.LastUsed.ToLocalTime().Date)
            .OrderByDescending(group => group.Key)
            .Select(group => (group.Key, group.ToList()))
            .ToList();

        HashSet<DateTime> desiredDays = [.. desired.Select(g => g.Date)];
        for (int i = GroupedEntries.Count - 1; i >= 0; i--)
        {
            if (!desiredDays.Contains(GroupedEntries[i].Date))
                GroupedEntries.RemoveAt(i);
        }

        for (int desiredIndex = 0; desiredIndex < desired.Count; desiredIndex++)
        {
            (DateTime date, List<ProjectHistoryEntry> entries) = desired[desiredIndex];

            int currentIndex = -1;
            for (int i = 0; i < GroupedEntries.Count; i++)
            {
                if (GroupedEntries[i].Date == date)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex == -1)
            {
                GroupedEntries.Insert(desiredIndex, new ProjectHistoryEntryGroup(GetGroupHeader(date), date, entries));
            }
            else
            {
                if (currentIndex != desiredIndex)
                    GroupedEntries.Move(currentIndex, desiredIndex);

                SyncGroupEntries(GroupedEntries[desiredIndex], entries);
            }
        }
    }

    private static void SyncGroupEntries(ProjectHistoryEntryGroup group, List<ProjectHistoryEntry> source)
    {
        // remove entries that moved to another day or disappeared
        HashSet<Guid> sourceIds = source.Select(e => e.Id).ToHashSet();
        for (int i = group.Count - 1; i >= 0; i--)
        {
            if (!sourceIds.Contains(group[i].Id))
                group.RemoveAt(i);
        }

        // insert or move entries to match desired order
        for (int desiredIndex = 0; desiredIndex < source.Count; desiredIndex++)
        {
            int currentIndex = -1;
            for (int i = 0; i < group.Count; i++)
            {
                if (group[i].Id == source[desiredIndex].Id)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex == -1)
                group.Insert(desiredIndex, source[desiredIndex]);
            else if (currentIndex != desiredIndex)
                group.Move(currentIndex, desiredIndex);
        }
    }

    private static string GetGroupHeader(DateTime localDay)
    {
        DateTime today = DateTime.Now.Date;

        if (localDay == today)
            return Resources.Strings.Resources.HistoryGroupToday;

        if (localDay == today.AddDays(-1))
            return Resources.Strings.Resources.HistoryGroupYesterday;

        return localDay.ToString("D", CultureInfo.CurrentCulture);
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
