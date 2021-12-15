public static class Enums
{
    /// <summary>
    ///     The possible scanner sources.
    /// </summary>
    public enum ScannerSource
    {
        None = 0,
        Auto = 1,
        Flatbed = 2,
        Feeder = 3
    }

    /// <summary>
    ///     The possible scanner color modes.
    /// </summary>
    public enum ScannerColorMode
    {
        None = 0,
        Color = 1,
        Grayscale = 2,
        Monochrome = 3,
        Automatic = 4
    }

    /// <summary>
    ///     The possible scanner auto crop modes.
    /// </summary>
    public enum ScannerAutoCropMode
    {
        None = 0,
        Disabled = 1,
        SingleRegion = 2,
        MultipleRegions = 3
    }

    /// <summary>
    ///     Available help topics.
    /// </summary>
    public enum HelpTopic
    {
        ScannerDiscovery,
        ScannerNotWorking,
        ChooseResolution,
        BrightnessContrast,
        AutoCrop,
        SaveChanges,
        ChangeScanFolder,
        ChooseFileFormat,
        StartNewPdf,
        ReorderPdfPages
    }

    /// <summary>
    ///     Available settings section.
    /// </summary>
    public enum SettingsSection
    {
        SaveLocation,
        AutoRotation,
        FileNaming,
        ScanOptions,
        ScanAction,
        Theme,
        EditorOrientation,
        Animations,
        ErrorReports,
        Surveys
    }
}