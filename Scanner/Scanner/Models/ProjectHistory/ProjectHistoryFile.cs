using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace Scanner.Models;

public partial class ProjectHistoryFile
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;

    // Foreign key
    public Guid ProjectHistoryEntryId { get; set; }
    public ProjectHistoryEntry ProjectHistoryEntry { get; set; }
}