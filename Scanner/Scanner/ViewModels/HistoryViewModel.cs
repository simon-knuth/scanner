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
    #endregion

    #region Commands
    public AsyncRelayCommand<ProjectHistoryEntry> RemoveEntryAsyncCommand;
    public AsyncRelayCommand ClearListAsyncCommand;
    public AsyncRelayCommand<ProjectHistoryEntry> ShowInFileExplorerAsyncCommand;
    public RelayCommand DisposeCommand => new RelayCommand(Dispose);
    #endregion


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public HistoryViewModel()
    {
        RemoveEntryAsyncCommand = new(RemoveEntryAsync);
        ClearListAsyncCommand = new(ClearListAsync);
        ShowInFileExplorerAsyncCommand = new(ShowInFileExplorerAsync);
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Dispose()
    {
        Messenger.UnregisterAll(this);
    }

    public async Task RemoveEntryAsync(ProjectHistoryEntry entry)
    {
        await ProjectHistoryService.RemoveEntryAsync(entry.Id);
    }

    public async Task ClearListAsync()
    {
        await ProjectHistoryService.ClearHistoryAsync();
    }

    private async Task ShowInFileExplorerAsync(ProjectHistoryEntry entry)
    {
        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(entry.Files[0].FilePath));
        await Windows.System.Launcher.LaunchFolderAsync(folder);
    }
}
