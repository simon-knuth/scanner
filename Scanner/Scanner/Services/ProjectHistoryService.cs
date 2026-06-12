using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualBasic;
using Scanner.Data;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Scanner.Services;

internal class ProjectHistoryService : IProjectHistoryService
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    #endregion

    #region Events
    public event EventHandler? HistoryChanged;
    #endregion

    #region Constants
    private const string DatabasesFolderName = "Databases";
    private const string DatabaseFileName = "ProjectHistory.db";
    private const int MaxHistoryEntries = 30;
    #endregion

    public ObservableCollection<ProjectHistoryEntry> Entries { get; } = [];

    private readonly SemaphoreSlim dbLock = new(1, 1);
    private TaskCompletionSource initializationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? databasePath;

    private DispatcherQueue? uiDispatcherQueue;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ProjectHistoryService() { }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public async Task InitializeAsync(DispatcherQueue uiDispatcherQueue)
    {
        LogService?.Log.Information("Initializing");
        this.uiDispatcherQueue = uiDispatcherQueue;

        try
        {
            StorageFolder localFolder = ApplicationData.Current.LocalCacheFolder;
            StorageFolder databasesFolder = await localFolder.CreateFolderAsync(
                DatabasesFolderName,
                CreationCollisionOption.OpenIfExists);

            databasePath = Path.Combine(databasesFolder.Path, DatabaseFileName);

            await using ProjectHistoryDbContext context = CreateContext();
            await context.Database.EnsureCreatedAsync();

            LogService?.Log.Information("Initialized database");

            // Populate the observable collection from the initial DB state.
            IReadOnlyList<ProjectHistoryEntry> initial = await FetchEntriesFromDbAsync(context);
            uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
            {
                Entries.Clear();
                foreach (ProjectHistoryEntry entry in initial)
                    Entries.Add(entry);
            });

            initializationTcs.TrySetResult();
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to initialize database");
            initializationTcs.TrySetException(exc);
            throw;
        }
    }

    public async Task<IReadOnlyList<ProjectHistoryEntry>> GetHistoryAsync()
    {
        await initializationTcs.Task;

        await dbLock.WaitAsync();
        try
        {
            await using ProjectHistoryDbContext context = CreateContext();
            return await FetchEntriesFromDbAsync(context);
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to retrieve project history");
            await RecreateAsync();
            return [];
        }
        finally
        {
            dbLock.Release();
        }
    }

    public async Task AddOrUpdateEntryAsync(ProjectBase project)
    {
        List<string> filePaths;
        if (project is PdfProject pdfProject)
        {
            if (pdfProject.TargetFile == null)
                return;

            filePaths = [pdfProject.TargetFile.File.Path];
        }
        else if (project is MultiFileProject multiFileProject)
        {
            if (multiFileProject.Pages.Any(x => x is ImagePage imagePage && imagePage.TargetFile == null))
                return;

            filePaths = [];
            foreach (IProjectPage page in multiFileProject.Pages)
            {
                if (page is ImagePage imagePage)
                    filePaths.Add(imagePage.TargetFile!.File.Path);
            }
        }
        else
        {
            throw new ArgumentException("Project type is not supported");
        }

        await initializationTcs.Task;

        await dbLock.WaitAsync();
        try
        {
            await using ProjectHistoryDbContext context = CreateContext();

            ProjectHistoryEntry? existing = await context.ProjectHistoryEntries
                .Include(e => e.Files)
                .FirstOrDefaultAsync(e => e.Id == project.Id);

            if (existing is not null)
            {
                existing.LastUsed = DateTime.UtcNow;
                existing.Format = project.Format;
                existing.Files = [.. filePaths.Select(path => new ProjectHistoryFile { FilePath = path })];
            }
            else
            {
                existing = new ProjectHistoryEntry
                {
                    Id = project.Id,
                    Format = project.Format,
                    LastUsed = DateTime.UtcNow,
                    Files = filePaths.Select(path => new ProjectHistoryFile { FilePath = path }).ToList()
                };

                await context.ProjectHistoryEntries.AddAsync(existing);

                // enforce limit
                int count = await context.ProjectHistoryEntries.CountAsync();
                if (count >= MaxHistoryEntries)
                {
                    List<ProjectHistoryEntry> overflow = await context.ProjectHistoryEntries
                        .OrderBy(e => e.LastUsed)
                        .Take(count - MaxHistoryEntries + 1)
                        .ToListAsync();

                    context.ProjectHistoryEntries.RemoveRange(overflow);
                }
            }

            await context.SaveChangesAsync();
            LogService?.Log.Information("Saved project {Id} to history", project.Id);

            IReadOnlyList<ProjectHistoryEntry> updated = await FetchEntriesFromDbAsync(context);
            uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
            {
                SyncHistory(updated);
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to add or update project {Id} in history", project.Id);
            await RecreateAsync();
        }
        finally
        {
            dbLock.Release();
        }
    }

    public async Task RemoveEntryAsync(Guid id)
    {
        await initializationTcs.Task;

        await dbLock.WaitAsync();
        try
        {
            await using ProjectHistoryDbContext context = CreateContext();

            ProjectHistoryEntry? entry = await context.ProjectHistoryEntries
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry is not null)
            {
                context.ProjectHistoryEntries.Remove(entry);
                await context.SaveChangesAsync();
                LogService?.Log.Information("Removed project {Id} from history", id);

                uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
                {
                    ProjectHistoryEntry? inCollection = Entries.FirstOrDefault(e => e.Id == id);
                    if (inCollection is not null)
                        Entries.Remove(inCollection);

                    HistoryChanged?.Invoke(this, EventArgs.Empty);
                });
            }
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to remove project {Id} from history", id);
            await RecreateAsync();
        }
        finally
        {
            dbLock.Release();
        }
    }

    public async Task ClearHistoryAsync()
    {
        await initializationTcs.Task;

        await dbLock.WaitAsync();
        try
        {
            await using ProjectHistoryDbContext context = CreateContext();
            await context.ProjectHistoryEntries.ExecuteDeleteAsync();
            LogService?.Log.Information("Cleared project history");

            uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
            {
                Entries.Clear();
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to clear project history");
            await RecreateAsync();
        }
        finally
        {
            dbLock.Release();
        }
    }

    private static async Task<IReadOnlyList<ProjectHistoryEntry>> FetchEntriesFromDbAsync(
        ProjectHistoryDbContext context)
    {
        return await context.ProjectHistoryEntries
            .Include(e => e.Files)
            .OrderByDescending(e => e.LastUsed)
            .Take(MaxHistoryEntries)
            .AsNoTracking()
            .ToListAsync();
    }

    private void SyncHistory(IReadOnlyList<ProjectHistoryEntry> source)
    {
        // remove entries
        HashSet<Guid> sourceIds = source.Select(e => e.Id).ToHashSet();
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            if (!sourceIds.Contains(Entries[i].Id))
                Entries.RemoveAt(i);
        }

        // insert or move entries
        for (int desiredIndex = 0; desiredIndex < source.Count; desiredIndex++)
        {
            int currentIndex = -1;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Id == source[desiredIndex].Id)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex == -1)
            {
                // new entry ~> insert
                Entries.Insert(desiredIndex, source[desiredIndex]);
            }
            else if (currentIndex != desiredIndex)
            {
                // existing entry ~> move it
                Entries[desiredIndex].LastUsed = source[currentIndex].LastUsed;
                Entries[desiredIndex].Format = source[currentIndex].Format;
                Entries[desiredIndex].Files = source[currentIndex].Files;
                Entries.Move(currentIndex, desiredIndex);
            }
            else
            {
                Entries[desiredIndex].LastUsed = source[currentIndex].LastUsed;
                Entries[desiredIndex].Format = source[currentIndex].Format;
                Entries[desiredIndex].Files = source[currentIndex].Files;
            }
        }
    }
    private async Task RecreateAsync()
    {
        LogService?.Log.Warning("Recreating database");

        try
        {
            if (databasePath is not null && File.Exists(databasePath))
                File.Delete(databasePath);

            await using ProjectHistoryDbContext context = CreateContext();
            await context.Database.EnsureCreatedAsync();

            initializationTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            initializationTcs.TrySetResult();

            uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
            {
                Entries.Clear();
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            });

            LogService?.Log.Warning("Database recreated successfully");
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to recreate database");
            initializationTcs.TrySetException(exc);
            throw;
        }
    }

    private ProjectHistoryDbContext CreateContext() => new(databasePath!);

    public async Task UpdateMissingFilesAsync()
    {
        await initializationTcs.Task;

        try
        {
            foreach (ProjectHistoryEntry entry in Entries)
            {
                try
                {
                    foreach (ProjectHistoryFile file in entry.Files)
                    {
                        await StorageFile.GetFileFromPathAsync(file.FilePath);
                    }

                    uiDispatcherQueue!.RunOnThread(DispatcherQueuePriority.Low, () => entry.AreFilesMissing = false);
                }
                catch (Exception)
                {
                    uiDispatcherQueue!.RunOnThread(DispatcherQueuePriority.Low, () => entry.AreFilesMissing = true);
                }
            }
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to clear project history");
            await RecreateAsync();
        }
    }
}