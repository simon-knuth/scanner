using CommunityToolkit.Mvvm.ComponentModel;
using Scanner.Data;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scanner.Models;

/// <summary>
/// A saved set of <see cref="ScanOptions"/> that the user can re-apply to a scan.
/// </summary>
[ObservableObject]
public partial class TemplateEntry : ScanOptionsSnapshot
{
    public Guid Id { get; set; }

    [ObservableProperty]
    private string name = string.Empty;

    /// <summary>UTC timestamp of when the template was created.</summary>
    public DateTime Created { get; set; }

    public string CreatedString => Created.ToLocalTime().ToString("g");

    /// <summary>UTC timestamp of when the template was last applied.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastUsedString))]
    private DateTime lastUsed;

    public string LastUsedString => LastUsed.ToLocalTime().ToString("g");

    [ObservableProperty]
    [property: NotMapped]
    private bool isRenaming;
}
