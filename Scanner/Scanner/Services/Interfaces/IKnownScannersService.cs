using Scanner.Data;
using Scanner.Models;
using Scanner.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scanner.Services.Interfaces;

/// <summary>
/// Keeps track of previously used scanners and corresponding last used <see cref="ScanOptions"/>.
/// </summary>
public interface IKnownScannersService
{
    #region Events
    event EventHandler UpdatedEntry;
    #endregion

    Task InitializeAsync();

    /// <summary>
    /// Returns all known scanners ordered by <see cref="KnownScannerEntry.LastUsed"/>
    /// descending (most recently used first).
    /// </summary>
    Task<IReadOnlyList<KnownScannerEntry>> GetKnownScannersAsync();

    /// <summary>
    /// Returns the entry for <paramref name="scannerId"/>, or null
    /// if the scanner has never been used before.
    /// </summary>
    Task<KnownScannerEntry?> GetEntryAsync(string scannerId);

    /// <summary>
    /// Called after every successful scan. Creates or updates the entry for the
    /// given scanner and snapshots the current <paramref name="options"/>.
    /// </summary>
    Task RecordScannerUsageAsync(IScanningDevice scanner, ScanOptions options);
}