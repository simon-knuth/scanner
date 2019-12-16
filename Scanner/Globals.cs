using Windows.Storage;
using Windows.UI.ViewManagement;

using static Enums;

public static class Globals
{
    public static Theme settingAppTheme;                            // user theme setting
    public static bool settingSearchIndicator;                      // whether the permanent search indicator should be visible
    public static bool settingAutomaticScannerSelection;            // automatically select first available scanner
    public static bool settingNotificationScanComplete;             // notify user when scan is complete if app is in the background
    public static bool settingUnsupportedFileFormat;                // allow unsupported file formats through conversion
    public static bool settingDrawPenDetected;                      // automatically start drawing when a pen is detected

    public static StorageFolder scanFolder = null;
    public static ApplicationDataContainer localSettingsContainer;
    public static ApplicationViewTitleBar applicationViewTitlebar;

    public static bool formatSettingChanged = false;
    public static bool possiblyDeadScanner = false;
    public static bool imageLoading = false;

    public static bool? firstAppLaunchWithThisVersion;
    public static int scanNumber;
}