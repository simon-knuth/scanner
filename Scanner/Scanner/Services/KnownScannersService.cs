using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Scanner.Data;
using Scanner.Helpers;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;

namespace Scanner.Services;

internal class KnownScannersService : IKnownScannersService
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
    #endregion

    #region Events
    public event EventHandler? UpdatedEntry;
    #endregion

    #region Constants
    private const string DatabasesFolderName = "Databases";
    private const string DatabaseFileName = "KnownScanners.db";
    #endregion

    private readonly SemaphoreSlim dbLock = new(1, 1);
    private TaskCompletionSource initializationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? databasePath;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public KnownScannersService()
    {
        SettingsService.PropertyChanged += SettingsService_PropertyChanged;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public async Task InitializeAsync()
    {
        LogService?.Log.Information("Initializing");

        try
        {
            StorageFolder localFolder = ApplicationData.Current.LocalCacheFolder;
            StorageFolder databasesFolder = await localFolder.CreateFolderAsync(
                DatabasesFolderName,
                CreationCollisionOption.OpenIfExists);

            databasePath = Path.Combine(databasesFolder.Path, DatabaseFileName);

            await using KnownScannersDbContext context = CreateContext();
            await context.Database.EnsureCreatedAsync();

            LogService?.Log.Information("Initialized database at '{Path}'", databasePath);

            initializationTcs.TrySetResult();
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to initialize database");
            initializationTcs.TrySetException(exc);
            throw;
        }
    }

    public async Task<IReadOnlyList<KnownScannerEntry>> GetKnownScannersAsync()
    {
        await initializationTcs.Task;

        await dbLock.WaitAsync();
        try
        {
            await using KnownScannersDbContext context = CreateContext();
            return await GetAllEntriesAsync(context);
        }
        catch (Exception exc)
        {
            LogService?.Log.Warning(exc, "Failed to retrieve known scanners");
            await RecreateAsync();
            return [];
        }
        finally
        {
            dbLock.Release();
        }
    }

    public async Task<KnownScannerEntry?> GetEntryAsync(string scannerId)
    {
        await initializationTcs.Task;

        await dbLock.WaitAsync();
        try
        {
            await using KnownScannersDbContext context = CreateContext();
            return await context.KnownScannerEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == scannerId);
        }
        catch (Exception exc)
        {
            LogService?.Log.Warning(exc, "Failed to retrieve entry for scanner {Id}", scannerId);
            await RecreateAsync();
            return null;
        }
        finally
        {
            dbLock.Release();
        }
    }

    public async Task RecordScannerUsageAsync(IScanningDevice scanner, ScanOptions options)
    {
        await initializationTcs.Task;

        await dbLock.WaitAsync();
        try
        {
            await using KnownScannersDbContext context = CreateContext();

            KnownScannerEntry? existing = await context.KnownScannerEntries
                .FirstOrDefaultAsync(e => e.Id == scanner.Id);

            if (existing is not null)
            {
                ApplyOptions(existing, scanner, options);
            }
            else
            {
                existing = new KnownScannerEntry { Id = scanner.Id };
                ApplyOptions(existing, scanner, options);
                await context.KnownScannerEntries.AddAsync(existing);
            }

            await context.SaveChangesAsync();
            UpdatedEntry?.Invoke(this, EventArgs.Empty);
            LogService?.Log.Information("Recorded usage for scanner {Id} ('{Name}')", scanner.Id, scanner.Name);
        }
        catch (Exception exc)
        {
            LogService?.Log.Warning(exc, "Failed to record usage for scanner {Id}", scanner.Id);
            await RecreateAsync();
        }
        finally
        {
            dbLock.Release();
        }
    }

    private static Task<List<KnownScannerEntry>> GetAllEntriesAsync(KnownScannersDbContext context)
    {
        return context.KnownScannerEntries
            .OrderByDescending(e => e.LastUsed)
            .AsNoTracking()
            .ToListAsync();
    }

    private static void ApplyOptions(KnownScannerEntry entry, IScanningDevice scanner, ScanOptions options)
    {
        entry.Name = scanner.Name;
        entry.LastUsed = DateTime.UtcNow;
        entry.CaptureOptions(options);
    }

    private async Task RecreateAsync()
    {
        LogService?.Log.Information("Recreating database at '{Path}'", databasePath);

        try
        {
            if (databasePath is not null && File.Exists(databasePath))
                File.Delete(databasePath);

            await using KnownScannersDbContext context = CreateContext();
            await context.Database.EnsureCreatedAsync();

            initializationTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            initializationTcs.TrySetResult();

            LogService?.Log.Warning("Database recreated successfully");
        }
        catch (Exception exc)
        {
            LogService?.Log.Error(exc, "Failed to recreate database");
            initializationTcs.TrySetException(exc);
            throw;
        }
    }

    private async Task ClearAllSavedOptionsAsync()
    {
        await initializationTcs.Task;

        await dbLock.WaitAsync();
        try
        {
            await using KnownScannersDbContext context = CreateContext();

            await context.KnownScannerEntries.ExecuteUpdateAsync(setters =>
                ScanOptionsSnapshot.AddClearSnapshotSetters(setters));

            LogService?.Log.Information("Cleared saved scan options for all scanners");
            UpdatedEntry?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exc)
        {
            LogService?.Log.Warning(exc, "Failed to clear saved scan options");
            await RecreateAsync();
        }
        finally
        {
            dbLock.Release();
        }
    }

    private async void SettingsService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ISettingsService.SettingRememberScanOptions):
                if (!SettingsService.SettingRememberScanOptions)
                    await ClearAllSavedOptionsAsync();
                break;
        }
    }

    private KnownScannersDbContext CreateContext() => new(databasePath!);
}