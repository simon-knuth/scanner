using Scanner.Models.Interfaces;
using System;

namespace Scanner.Data;

/// <summary>
/// <see cref="IScanningDevice"/> that's been used at least once.
/// </summary>
public class KnownScannerEntry : ScanOptionsSnapshot
{
    /// <summary>Maps to <see cref="IScanningDevice.Id"/>.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the last successful scan. Used to rank preferred scanners.</summary>
    public DateTime LastUsed { get; set; }
}

public enum ScanAreaKind
{
    None = 0,
    AutoCrop = 1,
    PaperSize = 2,
    PreviewSelection = 3
}
