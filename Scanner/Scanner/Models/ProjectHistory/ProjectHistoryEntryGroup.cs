using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Scanner.Models;

/// <summary>
///     A set of <see cref="ProjectHistoryEntry"/>s that share the same calendar day. Used to
///     present the project history grouped by date.
/// </summary>
public class ProjectHistoryEntryGroup : ObservableCollection<ProjectHistoryEntry>
{
    /// <summary>
    ///    Localized header shown for this group (e.g. "Today", "Yesterday" or a date).
    /// </summary>
    public string Key { get; }

    /// <summary>
    ///     The local calendar day shared by all entries in this group.
    /// </summary>
    public DateTime Date { get; }

    public ProjectHistoryEntryGroup(string key, DateTime date, IEnumerable<ProjectHistoryEntry> entries)
        : base(entries)
    {
        Key = key;
        Date = date;
    }
}
