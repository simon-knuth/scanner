using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.ViewManagement;

public static class Globals
{
    public static Theme settingAppTheme;                            // user theme setting
    public static bool settingSearchIndicator;                      // whether the permanent search indicator should be visible
    public static bool settingAutomaticScannerSelection;            // automatically select first available scanner
    public static bool settingNotificationScanComplete;             // notify user when scan is complete if app is in the background
    public static bool settingUnsupportedFileFormat;                // allow unsupported file formats through conversion

    public static StorageFolder scanFolder = null;
    public static ApplicationDataContainer localSettingsContainer;
    public static ApplicationViewTitleBar applicationViewTitlebar;
    public static StorageItemAccessList futureAccessList = StorageApplicationPermissions.FutureAccessList;

    public static bool formatSettingChanged = false;
    public static bool possiblyDeadScanner = false;

    public static bool? firstAppLaunchWithThisVersion;
    public static int scanNumber;



    public enum Theme {
        system = 0,
        light = 1,
        dark = 2
    }
}