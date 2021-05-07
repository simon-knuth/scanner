public static class Enums
{
    /// <summary>
    ///     Represents the possible states of the app's UI.
    /// </summary>
    public enum UIstate
    {
        unset = -1,
        small = 0,                  // the whole UI is visible
        normal = 1,                   // the small UI is visible
        wide = 2                    // the wide UI is visible
    }


    /// <summary>
    ///     Represents the possible states of the app itself.
    /// </summary>
    public enum FlowState
    {
        initial = 0,                // there is nothing out of the ordinary happening
        scanning = 1,               // there is a scan in progress
        select = 2,                 // there is a result visible and scans being selected
        crop = 3,                   // there is a result visible and being cropped
        draw = 4                    // there is a result visible and being used for inking
    }


    /// <summary>
    ///     Represents the possible states of the primary <see cref="CommandBar"/>.
    /// </summary>
    public enum PrimaryMenuConfig
    {
        hidden = 0,                 // the primary CommandBar is hidden
        image = 1,                  // the primary CommandBar shows the image commands
        pdf = 2                     // the primaryCommandBar shows the pdf commands
    }


    /// <summary>
    ///     Represents the possible states of the secondary <see cref="CommandBar"/>.
    /// </summary>
    public enum SecondaryMenuConfig
    {
        hidden = 0,                 // the secondary CommandBar is hidden
        done = 1,                   // the secondary CommandBar shows the "done" button
        crop = 2,                   // the secondary CommandBar shows the crop commands
        draw = 3                    // the secondary CommandBar shows the draw commands
    }


    /// <summary>
    ///     Represents the possible themes.
    /// </summary>
    public enum Theme
    {
        system = 0,
        light = 1,
        dark = 2
    }


    /// <summary>
    ///     Represents the supported formats for various tasks.
    /// </summary>
    public enum SupportedFormat
    {
        JPG = 0,
        PNG = 1,
        TIF = 2,
        BMP = 3,
        PDF = 4,
        XPS = 5,
        OpenXPS = 6,
    }


    /// <summary>
    ///     Represents the different modes for the editing toolbar.
    /// </summary>
    public enum SummonToolbar
    {
        Hidden = 0,
        Crop = 1,
        Draw = 2,
    }


    /// <summary>
    ///     Represents the different actions that can request a scope selection.
    /// </summary>
    public enum ScopeActions
    {
        Copy = 0,
        OpenWith = 1,
        Share = 2,
    }


    /// <summary>
    ///     Represents the different source modes that a scanner can support.
    /// </summary>
    public enum SourceMode
    {
        Auto = 0,
        Flatbed = 1,
        Feeder = 2,
    }


    public enum ThirdPartyLicense
    {
        MicrosoftAppCenterAnalytics = 0,
        MicrosoftAppCenterCrashes = 1,
        MicrosoftNETCoreUniversalWindowsPlatform = 2,
        MicrosoftServicesStoreEngagement = 3,
        MicrosoftToolkitUwpNotifications = 4,
        MicrosoftToolkitUwpUIAnimations = 5,
        MicrosoftToolkitUwpUIControls = 6,
        MicrosoftToolkitUwpUILottie = 7,
        MicrosoftUIXAML = 8,
        PDFsharp = 9,
        QueryStringNET = 10,
        Win2Duwp = 11,
        Serilog = 12,
        SerilogExceptions = 13,
        SerilogSinksAsync = 14,
        SerilogSinksFile = 15,
    }
}