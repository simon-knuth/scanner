using Microsoft.UI.Dispatching;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Scanner.Services.Interfaces;

/// <summary>
/// Keeps track of user-saved <see cref="ScanOptions"/> templates.
/// </summary>
internal interface ITemplatesService
{
    event EventHandler TemplatesChanged;

    ObservableCollection<TemplateEntry> Entries { get; }

    /// <summary>
    ///     Initializes the database, creating or migrating it as needed.
    /// </summary>
    Task InitializeAsync(DispatcherQueue dispatcherQueue);

    /// <summary>
    ///     Returns all templates, ordered by <see cref="TemplateEntry.LastUsed"/> descending.
    /// </summary>
    Task<IReadOnlyList<TemplateEntry>> GetTemplatesAsync();

    /// <summary>
    ///     Creates a new template by capturing the given <paramref name="options"/>.
    /// </summary>
    Task<TemplateEntry?> AddTemplateAsync(string name, ScanOptions options);

    /// <summary>
    ///     Replaces an existing template's stored options with a new snapshot.
    /// </summary>
    Task UpdateTemplateAsync(TemplateEntry template, string name, ScanOptions options);

    /// <summary>
    ///     Renames an existing template.
    /// </summary>
    Task RenameTemplateAsync(TemplateEntry template, string name);

    /// <summary>
    ///     Bumps the <see cref="TemplateEntry.LastUsed"/> timestamp on a template.
    /// </summary>
    Task MarkTemplateUsedAsync(TemplateEntry template);

    /// <summary>
    ///     Removes a single template.
    /// </summary>
    Task RemoveTemplateAsync(TemplateEntry template);

    /// <summary>
    ///     Clears all templates.
    /// </summary>
    Task ClearTemplatesAsync();
}
