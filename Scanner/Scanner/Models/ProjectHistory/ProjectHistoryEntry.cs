using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;

namespace Scanner.Models;

public partial class ProjectHistoryEntry : ObservableObject
{
    public Guid Id { get; set; }
    public TargetFormat Format { get; set; }
    public DateTime LastUsed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FriendlyName))]
    [NotifyPropertyChangedFor(nameof(FriendlyNameAnnotation))]
    private List<ProjectHistoryFile> files;

    public string FriendlyName => Path.GetFileNameWithoutExtension(Files[0].FilePath);
    public string FriendlyNameAnnotation => Files.Count > 1 ? $"+{Files.Count - 1} more" : string.Empty;
}