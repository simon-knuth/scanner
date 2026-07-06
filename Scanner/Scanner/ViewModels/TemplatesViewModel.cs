using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Scanner.Messages;
using Scanner.Models;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Scanner.ViewModels;

partial class TemplatesViewModel : ObservableRecipient, IDisposable
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    public readonly ITemplatesService TemplateService = Ioc.Default.GetRequiredService<ITemplatesService>();
    private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
    private readonly ISentryService? SentryService = Ioc.Default.GetService<ISentryService>();
    #endregion

    #region Commands
    public RelayCommand<TemplateEntry> TryApplyTemplateCommand => new(TryApplyTemplate);
    public AsyncRelayCommand CreateTemplateAsyncCommand;
    public AsyncRelayCommand<TemplateEntry> RemoveTemplateAsyncCommand;
    public AsyncRelayCommand ClearListAsyncCommand;
    public RelayCommand<TemplateEntry> StartRenamingCommand => new(StartRenaming);
    public RelayCommand DisposeCommand => new RelayCommand(Dispose);
    #endregion

    [ObservableProperty]
    private bool isScannerSelected;

    public ObservableCollection<TemplateEntry> SortedEntries { get; } = [];

    // Entries whose PropertyChanged we're subscribed to, so a rename can re-order the list live.
    private readonly HashSet<TemplateEntry> subscribedEntries = [];

    public TemplateSortMode SortMode
    {
        get => SettingsService.SettingTemplateSortMode;
        set
        {
            if (SettingsService.SettingTemplateSortMode == value) return;

            SettingsService.SettingTemplateSortMode = value;
            RebuildSortedEntries();

            OnPropertyChanged(nameof(SortMode));
            OnPropertyChanged(nameof(SortByRecentlyUsed));
            OnPropertyChanged(nameof(SortByName));
        }
    }

    public bool SortByRecentlyUsed
    {
        get => SortMode == TemplateSortMode.RecentlyUsed;
        set { if (value) SortMode = TemplateSortMode.RecentlyUsed; }
    }

    public bool SortByName
    {
        get => SortMode == TemplateSortMode.Name;
        set { if (value) SortMode = TemplateSortMode.Name; }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public TemplatesViewModel()
    {
        CreateTemplateAsyncCommand = new(CreateTemplateAsync);
        RemoveTemplateAsyncCommand = new(RemoveTemplateAsync);
        ClearListAsyncCommand = new(ClearListAsync);

        Messenger.Register<SelectedScannerChangedMessage>(this, (r, m) =>
        {
            IsScannerSelected = m.SelectedScanner != null;
        });
        IsScannerSelected = Messenger.Send(new SelectedScannerRequestMessage()).Response != null;

        TemplateService.Entries.CollectionChanged += Entries_CollectionChanged;
        RebuildSortedEntries();

        SentryService?.TrackEvent(AnalyticsEvent.TemplatesViewOpened);
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Dispose()
    {
        TemplateService.Entries.CollectionChanged -= Entries_CollectionChanged;
        foreach (TemplateEntry entry in subscribedEntries)
        {
            entry.PropertyChanged -= Entry_PropertyChanged;
        }

        subscribedEntries.Clear();

        Messenger.UnregisterAll(this);
    }

    private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildSortedEntries();
    }

    private void Entry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (SortMode == TemplateSortMode.Name && e.PropertyName == nameof(TemplateEntry.Name))
            RebuildSortedEntries();
    }

    private void RebuildSortedEntries()
    {
        UpdatePropertyChangedSubscriptions();

        IEnumerable<TemplateEntry> source = TemplateService.Entries;
        List<TemplateEntry> desired = [.. SortMode switch
        {
            TemplateSortMode.Name => source.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ThenByDescending(e => e.LastUsed),
            _ => source.OrderByDescending(e => e.LastUsed),
        }];

        // remove entries no longer present
        HashSet<Guid> desiredIds = [.. desired.Select(e => e.Id)];
        for (int i = SortedEntries.Count - 1; i >= 0; i--)
        {
            if (!desiredIds.Contains(SortedEntries[i].Id))
                SortedEntries.RemoveAt(i);
        }

        // insert or move entries to match the desired order
        for (int desiredIndex = 0; desiredIndex < desired.Count; desiredIndex++)
        {
            int currentIndex = -1;
            for (int i = 0; i < SortedEntries.Count; i++)
            {
                if (SortedEntries[i].Id == desired[desiredIndex].Id)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex == -1)
                SortedEntries.Insert(desiredIndex, desired[desiredIndex]);
            else if (currentIndex != desiredIndex)
                SortedEntries.Move(currentIndex, desiredIndex);
        }
    }

    private void UpdatePropertyChangedSubscriptions()
    {
        foreach (TemplateEntry entry in subscribedEntries.Except(TemplateService.Entries).ToList())
        {
            entry.PropertyChanged -= Entry_PropertyChanged;
            subscribedEntries.Remove(entry);
        }

        foreach (TemplateEntry entry in TemplateService.Entries)
        {
            if (subscribedEntries.Add(entry))
                entry.PropertyChanged += Entry_PropertyChanged;
        }
    }

    public void TryApplyTemplate(TemplateEntry template)
    {
        SentryService?.TrackEvent(AnalyticsEvent.TemplateApplied);
        Messenger.Send(new ApplyTemplateMessage(template));
    }

    public async Task CreateTemplateAsync()
    {
        await TemplateService.AddTemplateAsync(Resources.Strings.Resources.Template, Messenger.Send(new ScanOptionsRequestMessage()).Response);
        SentryService?.TrackEvent(AnalyticsEvent.TemplateCreated);
    }

    public async Task RemoveTemplateAsync(TemplateEntry template)
    {
        await TemplateService.RemoveTemplateAsync(template);
        SentryService?.TrackEvent(AnalyticsEvent.TemplateRemoved);
    }

    public async Task ClearListAsync()
    {
        await TemplateService.ClearTemplatesAsync();
        SentryService?.TrackEvent(AnalyticsEvent.TemplatesCleared);
    }

    public void StartRenaming(TemplateEntry template)
    {
        template.IsRenaming = true;
    }

    public async Task StopRenamingAsync(TemplateEntry template, string name)
    {
        template.IsRenaming = false;
        template.Name = name;
        await TemplateService.RenameTemplateAsync(template, name);
        SentryService?.TrackEvent(AnalyticsEvent.TemplateRenamed);
    }
}
