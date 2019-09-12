public static class Enums
{
    /// <summary>
    ///     Represents the possible states of the app's UI.
    /// </summary>
    public enum UIstate
    {
        unset = -1,
        full = 0,                   // the whole UI is visible
        small_initial = 1,          // only the options pane is visible
        small_result = 2            // only the result of a scan is visible
    }


    /// <summary>
    ///     Represents the possible states of the app itself.
    /// </summary>
    public enum FlowState
    {
        initial = 0,                // there is no result visible
        result = 1,                 // there is a result visible but no crop in progress
        crop = 2,                   // there is a result visible and a crop in progress
        draw = 3                    // there is a result visible and drawing in progress
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
    }
