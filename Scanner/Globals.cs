using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.ViewManagement;

public static class Globals
{
    public static Theme settingAppTheme;
    public static bool settingSearchIndicator;
    public static bool settingNotificationScanComplete;
    public static bool settingUnsupportedFileFormat;
    public static bool formatSettingChanged = false;
    public static bool? firstAppLaunchWithThisVersion;
    public static StorageFolder scanFolder = null;
    public static ApplicationDataContainer localSettingsContainer;
    public static ApplicationViewTitleBar applicationViewTitlebar;
    public static StorageItemAccessList futureAccessList = StorageApplicationPermissions.FutureAccessList;
    public static bool autoSelectScanner = true;
    

    public enum Theme {
        system = 0,
        light = 1,
        dark = 2
    }
}