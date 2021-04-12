using Serilog;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.UI.ViewManagement;

using static Enums;

public static class Globals
{
    public static bool settingSaveLocationAsk;                      // ask user for save location after each scan
    public static Theme settingAppTheme;                            // user theme setting
    public static bool settingAppendTime;                           // append time to file names
    public static bool settingNotificationScanComplete;             // notify user when scan is complete if app is in the background
    public static bool settingErrorStatistics;                      // send reports to the Microsoft AppCenter

    public static ILogger log;

    public static StorageFolder scanFolder = null;
    public static ApplicationDataContainer localSettingsContainer;
    public static ApplicationViewTitleBar applicationViewTitlebar;

    public static bool possiblyDeadScanner = false;

    public static bool? isFirstAppLaunchWithThisVersion;
    public static int scanNumber;
    public static bool lastTouchDrawState;                          // whether drawing with touch was enabled last time
    public static bool manageTutorialAlreadyShown;

    public static AppServiceConnection appServiceConnection;
    public static BackgroundTaskDeferral appServiceDeferral;
    public static TaskCompletionSource<bool> taskCompletionSource = null;

    public static StorageFolder folderTemp;
    public static StorageFolder folderConversion;
    public static StorageFolder folderWithoutRotation;

    public static string storeRateUri = "ms-windows-store://review/?productid=9N438MZHD3ZF";

    public static string glyphButtonRecentsDefault = "\uE838";
    public static string glyphButtonRecentsCustom = "\uEC25";
    public static string glyphButtonRecentsHistory = "\uE81C";
    public static string glyphFormatImage = "\uEB9F";
    public static string glyphFormatPdf = "\uEA90";
}