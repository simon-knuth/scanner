using Windows.UI.ViewManagement;

public static class Globals
{
    public static Theme settingAppTheme;
    public static bool settingSearchIndicator;
    public static bool settingNotificationScanComplete;
    public static bool settingUnsupportedFileFormat;
    public static Windows.Storage.ApplicationDataContainer localSettingsContainer;
    public static ApplicationViewTitleBar applicationViewTitlebar;
    

    public enum Theme {
        system = 0,
        light = 1,
        dark = 2
    }
}