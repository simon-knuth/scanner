using Microsoft.UI.Dispatching;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Scanner.Services.Interfaces;

/// <summary>
/// Keeps track of past target files.
/// </summary>
internal interface IProjectHistoryService
{
    event EventHandler HistoryChanged;

    ObservableCollection<ProjectHistoryEntry> Entries { get; }

    /// <summary>
    ///     Initializes the database, creating or migrating it as needed.
    /// </summary>
    Task InitializeAsync(DispatcherQueue dispatcherQueue);

    /// <summary>
    ///     Returns recent project history entries, ordered by <see cref="ProjectHistoryEntry.LastUsed"/> descending.
    /// </summary>
    Task<IReadOnlyList<ProjectHistoryEntry>> GetHistoryAsync();

    /// <summary>
    ///     Adds a new project to the history or, if it already exists, moves it to the top.
    /// </summary>
    Task AddOrUpdateEntryAsync(ProjectBase project);

    /// <summary>
    ///     Removes a single project from the history.
    /// </summary>
    Task RemoveEntryAsync(Guid id);

    /// <summary>
    ///     Clears the entire project history.
    /// </summary>
    Task ClearHistoryAsync();

    /// <summary>
    /// Updates <see cref="ProjectHistoryEntry.AreFilesMissing"/> for all entries.
    /// </summary>
    Task UpdateMissingFilesAsync();
}