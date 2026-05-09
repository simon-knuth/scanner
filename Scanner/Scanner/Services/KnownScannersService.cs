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
        entry.LastSourceMode = options.SourceMode;
        entry.LastTargetFormat = options.TargetFormat;
        entry.LastColorMode = options.ColorMode;
        entry.LastDuplex = options.Duplex;
        entry.LastScanMultiplePages = options.ScanMultiplePages;
        entry.LastBrightness = options.Brightness;
        entry.LastContrast = options.Contrast;
        entry.LastResolutionDpiX = (float?)options.Resolution?.Resolution.DpiX;
        entry.LastResolutionDpiY = (float?)options.Resolution?.Resolution.DpiY;

        // ScanArea
        ClearScanAreaColumns(entry);

        switch (options.ScanArea)
        {
            case AutoCropArea autoCrop:
                entry.LastScanAreaKind = ScanAreaKind.AutoCrop;
                entry.LastAutoCropMode = autoCrop.AutoCropMode;
                break;

            case PaperSizeArea paperSize:
                entry.LastScanAreaKind = ScanAreaKind.PaperSize;
                entry.LastPaperSize = paperSize.PaperSize;
                entry.LastPaperSizeCorner = paperSize.Corner;
                entry.LastPaperSizeOrientation = paperSize.Orientation;
                break;

            case PreviewSelectionArea preview:
                entry.LastScanAreaKind = ScanAreaKind.PreviewSelection;
                entry.LastPreviewSelectionX = preview.SelectedRegion.X;
                entry.LastPreviewSelectionY = preview.SelectedRegion.Y;
                entry.LastPreviewSelectionWidth = preview.SelectedRegion.Width;
                entry.LastPreviewSelectionHeight = preview.SelectedRegion.Height;
                break;

            case null:
                entry.LastScanAreaKind = null;
                break;
        }
    }

    private static void ClearScanAreaColumns(KnownScannerEntry entry)
    {
        entry.LastScanAreaKind = null;
        entry.LastAutoCropMode = null;
        entry.LastPaperSize = null;
        entry.LastPaperSizeCorner = null;
        entry.LastPaperSizeOrientation = null;
        entry.LastPreviewSelectionX = null;
        entry.LastPreviewSelectionY = null;
        entry.LastPreviewSelectionWidth = null;
        entry.LastPreviewSelectionHeight = null;
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

            await context.KnownScannerEntries.ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.LastSourceMode, (ScannerSource?)null)
                .SetProperty(e => e.LastTargetFormat, (TargetFormat?)null)
                .SetProperty(e => e.LastColorMode, (ScannerColorMode?)null)
                .SetProperty(e => e.LastResolutionDpiX, (float?)null)
                .SetProperty(e => e.LastResolutionDpiY, (float?)null)
                .SetProperty(e => e.LastDuplex, (bool?)null)
                .SetProperty(e => e.LastScanMultiplePages, (bool?)null)
                .SetProperty(e => e.LastBrightness, (int?)null)
                .SetProperty(e => e.LastContrast, (int?)null)
                .SetProperty(e => e.LastScanAreaKind, (ScanAreaKind?)null)
                .SetProperty(e => e.LastAutoCropMode, (ScannerAutoCropMode?)null)
                .SetProperty(e => e.LastPaperSize, (PaperSize?)null)
                .SetProperty(e => e.LastPaperSizeCorner, (ScanCorner?)null)
                .SetProperty(e => e.LastPaperSizeOrientation, (ScanOrientation?)null)
                .SetProperty(e => e.LastPreviewSelectionX, (double?)null)
                .SetProperty(e => e.LastPreviewSelectionY, (double?)null)
                .SetProperty(e => e.LastPreviewSelectionWidth, (double?)null)
                .SetProperty(e => e.LastPreviewSelectionHeight, (double?)null));

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