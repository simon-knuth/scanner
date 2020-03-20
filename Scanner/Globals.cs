using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.UI.ViewManagement;

using static Enums;

public static class Globals
{
    public static Theme settingAppTheme;                            // user theme setting
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
    public static bool lastTouchDrawState;                          // whether drawing with touch was enabled last time 

    public static AppServiceConnection appServiceConnection;
    public static BackgroundTaskDeferral appServiceDeferral;
    public static TaskCompletionSource<bool> taskCompletionSource = null;


    public static string storeRateUri = "ms-windows-store://review/?productid=9N438MZHD3ZF";

    public static string glyphButtonRecentsDefault = "\uE838";
    public static string glyphButtonRecentsCustom = "\uEC25";
}