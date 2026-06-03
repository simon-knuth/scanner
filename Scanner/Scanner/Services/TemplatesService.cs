using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Dispatching;
using Scanner.Data;
using Scanner.Extensions;
using Scanner.Models;
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

internal class TemplatesService : ITemplatesService
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    #endregion

    #region Events
    public event EventHandler? TemplatesChanged;
    #endregion

    #region Constants
    private const string DatabasesFolderName = "Databases";
    private const string DatabaseFileName = "Templates.db";

    // Total attempts (initial + retries) for transient database contention before giving up.
    private const int MaxAttempts = 4;
    // Linear backoff base; attempt N waits N * this many milliseconds.
    private const int RetryBackoffMilliseconds = 50;

    // SQLite primary result codes that indicate transient contention, not corruption.
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;

    // Win32 file-lock HResults (ERROR_SHARING_VIOLATION / ERROR_LOCK_VIOLATION) surfaced as IOException.
    private const int HResultSharingViolation = unchecked((int)0x80070020);
    private const int HResultLockViolation = unchecked((int)0x80070021);
    #endregion

    public ObservableCollection<TemplateEntry> Entries { get; } = [];

    private readonly SemaphoreSlim dbLock = new(1, 1);
    private TaskCompletionSource initializationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? databasePath;

    private DispatcherQueue? uiDispatcherQueue;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public TemplatesService() { }


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

            await using TemplatesDbContext context = CreateContext();
            await context.Database.EnsureCreatedAsync();

            LogService?.Log.Information("Initialized database at '{Path}'", databasePath);

            IReadOnlyList<TemplateEntry> initial = await FetchEntriesFromDbAsync(context);
            uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
            {
                Entries.Clear();
                foreach (TemplateEntry entry in initial)
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

    public async Task<IReadOnlyList<TemplateEntry>> GetTemplatesAsync()
    {
        return await ExecuteAsync(FetchEntriesFromDbAsync, "retrieve templates") ?? [];
    }

    public Task<TemplateEntry?> AddTemplateAsync(string name, ScanOptions options)
    {
        return ExecuteAsync<TemplateEntry?>(async context =>
        {
            TemplateEntry entry = new()
            {
                Id = Guid.NewGuid(),
                Name = name,
                Created = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow,
            };
            entry.CaptureOptions(options);

            await context.TemplateEntries.AddAsync(entry);
            await context.SaveChangesAsync();
            LogService?.Log.Information("Added template {Id} ('{Name}')", entry.Id, entry.Name);

            IReadOnlyList<TemplateEntry> updated = await FetchEntriesFromDbAsync(context);
            uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
            {
                SyncEntries(updated);
                TemplatesChanged?.Invoke(this, EventArgs.Empty);
            });

            return entry;
        }, "add template");
    }

    public Task UpdateTemplateAsync(TemplateEntry template, string name, ScanOptions options)
    {
        return ExecuteAsync(async context =>
        {
            template.Name = name;
            template.LastUsed = DateTime.UtcNow;
            template.CaptureOptions(options);

            // The entry comes from the AsNoTracking-fetched Entries collection, so it is detached
            // from this freshly created context. Update() attaches it and marks every column
            // Modified, otherwise SaveChangesAsync would see no changes and persist nothing.
            context.TemplateEntries.Update(template);
            await context.SaveChangesAsync();
            LogService?.Log.Information("Updated template {Id} ('{Name}')", template.Id, template.Name);

            IReadOnlyList<TemplateEntry> updated = await FetchEntriesFromDbAsync(context);
            uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
            {
                SyncEntries(updated);
                TemplatesChanged?.Invoke(this, EventArgs.Empty);
            });
        }, $"update template {template.Id}");
    }

    public Task RenameTemplateAsync(TemplateEntry template, string name)
    {
        return ExecuteAsync(async context =>
        {
            // Set-based update writes directly without relying on change tracking — the detached
            // entry from the AsNoTracking-fetched Entries collection isn't tracked by this context.
            int affected = await context.TemplateEntries
                .Where(e => e.Id == template.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Name, name));

            LogService?.Log.Information(
                affected > 0 ? "Renamed template {Id} to '{Name}'" : "Template {Id} was missing",
                template.Id, name);

            IReadOnlyList<TemplateEntry> updated = await FetchEntriesFromDbAsync(context);
            uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
            {
                SyncEntries(updated);
                TemplatesChanged?.Invoke(this, EventArgs.Empty);
            });
        }, $"rename template {template.Id}");
    }

    public Task MarkTemplateUsedAsync(TemplateEntry template)
    {
        return ExecuteAsync(async context =>
        {
            DateTime now = DateTime.UtcNow;

            // Set-based update writes directly without relying on change tracking — the detached
            // entry from the AsNoTracking-fetched Entries collection isn't tracked by this context.
            await context.TemplateEntries
                .Where(e => e.Id == template.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.LastUsed, now));

            IReadOnlyList<TemplateEntry> updated = await FetchEntriesFromDbAsync(context);
            uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
            {
                SyncEntries(updated);
                TemplatesChanged?.Invoke(this, EventArgs.Empty);
            });
        }, $"mark template {template.Id} used");
    }

    public Task RemoveTemplateAsync(TemplateEntry template)
    {
        return ExecuteAsync(async context =>
        {
            // Set-based delete is idempotent: removing an already-removed template affects 0 rows
            // and returns quietly, rather than tracking the entity and throwing
            // DbUpdateConcurrencyException when the row is gone (e.g. a double-tapped delete).
            int affected = await context.TemplateEntries
                .Where(e => e.Id == template.Id)
                .ExecuteDeleteAsync();

            LogService?.Log.Information(
                affected > 0 ? "Removed template {Id}" : "Template {Id} was already removed",
                template.Id);

            uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
            {
                TemplateEntry? inCollection = Entries.FirstOrDefault(e => e.Id == template.Id);
                if (inCollection is not null)
                    Entries.Remove(inCollection);

                TemplatesChanged?.Invoke(this, EventArgs.Empty);
            });
        }, $"remove template {template.Id}");
    }

    public Task ClearTemplatesAsync()
    {
        return ExecuteAsync(async context =>
        {
            await context.TemplateEntries.ExecuteDeleteAsync();
            LogService?.Log.Information("Cleared all templates");

            uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
            {
                Entries.Clear();
                TemplatesChanged?.Invoke(this, EventArgs.Empty);
            });
        }, "clear templates");
    }

    /// <summary>
    /// Runs a database operation under the serializing <see cref="dbLock"/>, retrying transient
    /// SQLite contention (busy/locked) with a short backoff. Transient contention never triggers
    /// <see cref="RecreateAsync"/> — only genuine failures (e.g. corruption) do, so a momentary
    /// lock can no longer cascade into the database being deleted.
    /// </summary>
    private async Task<T?> ExecuteAsync<T>(Func<TemplatesDbContext, Task<T>> operation, string action)
    {
        await initializationTcs.Task;

        await dbLock.WaitAsync();
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await using TemplatesDbContext context = CreateContext();
                    return await operation(context);
                }
                catch (Exception exc) when (IsTransientContention(exc) && attempt < MaxAttempts)
                {
                    LogService?.Log.Warning(exc,
                        "Database busy while trying to {Action}; retrying (attempt {Attempt}/{Max})",
                        new object[] { action, attempt, MaxAttempts });
                    await Task.Delay(RetryBackoffMilliseconds * attempt);
                }
            }
        }
        catch (Exception exc) when (IsTransientContention(exc))
        {
            // Contention is transient by nature, so we leave the database untouched rather than recreate it.
            LogService?.Log.Error(exc,
                "Database still busy while trying to {Action} after {Max} attempts; leaving database intact",
                action, MaxAttempts);
            return default;
        }
        catch (DbUpdateException exc)
        {
            // The file is readable; the change simply didn't apply (e.g. the row was already deleted or
            // modified concurrently). Recreating the database here would wipe every template, so an update
            // failure is treated as a benign no-op and the data is left intact.
            LogService?.Log.Error(exc, "Database update failed while trying to {Action}", (object)action);
            return default;
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to {Action}", (object)action);
            await RecreateAsync();
            return default;
        }
        finally
        {
            dbLock.Release();
        }
    }

    private Task ExecuteAsync(Func<TemplatesDbContext, Task> operation, string action)
    {
        return ExecuteAsync<object?>(async context =>
        {
            await operation(context);
            return null;
        }, action);
    }

    private static bool IsTransientContention(Exception exc)
    {
        for (Exception? current = exc; current is not null; current = current.InnerException)
        {
            if (current is SqliteException sqlite
                && sqlite.SqliteErrorCode is SqliteBusy or SqliteLocked)
            {
                return true;
            }

            // A locked database file surfaces as a Win32 sharing/lock violation rather than a
            // SqliteException — also transient, so retry instead of recreating the database.
            if (current is IOException io
                && (io.HResult == HResultSharingViolation || io.HResult == HResultLockViolation))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<IReadOnlyList<TemplateEntry>> FetchEntriesFromDbAsync(TemplatesDbContext context)
    {
        return await context.TemplateEntries
            .OrderByDescending(e => e.LastUsed)
            .AsNoTracking()
            .ToListAsync();
    }

    private void SyncEntries(IReadOnlyList<TemplateEntry> source)
    {
        HashSet<Guid> sourceIds = source.Select(e => e.Id).ToHashSet();
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            if (!sourceIds.Contains(Entries[i].Id))
                Entries.RemoveAt(i);
        }

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
                Entries.Insert(desiredIndex, source[desiredIndex]);
            }
            else
            {
                CopyFields(source[desiredIndex], Entries[currentIndex]);
                if (currentIndex != desiredIndex)
                    Entries.Move(currentIndex, desiredIndex);
            }
        }
    }

    private static void CopyFields(TemplateEntry source, TemplateEntry target)
    {
        target.Name = source.Name;
        target.LastUsed = source.LastUsed;
        target.CopySnapshotFrom(source);
    }

    private async Task RecreateAsync()
    {
        LogService?.Log.Warning("Recreating database at '{Path}'", databasePath);

        try
        {
            // Release any handles still held by pooled connections before deleting the file.
            SqliteConnection.ClearAllPools();

            if (databasePath is not null && File.Exists(databasePath))
                File.Delete(databasePath);

            await using TemplatesDbContext context = CreateContext();
            await context.Database.EnsureCreatedAsync();

            initializationTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            initializationTcs.TrySetResult();

            uiDispatcherQueue.RunOnThread(DispatcherQueuePriority.Low, () =>
            {
                Entries.Clear();
                TemplatesChanged?.Invoke(this, EventArgs.Empty);
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

    private TemplatesDbContext CreateContext() => new(databasePath!);
}
