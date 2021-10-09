using Serilog;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using static Enums_old;

public static class Globals
{
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
    public static StorageFolder folderReceivedPagesPDF;

    public static SpeechSynthesizer narratorSpeech = new SpeechSynthesizer();
    public static MediaElement narratorMediaElement = new MediaElement();

    public static string storeRateUri = "ms-windows-store://review/?productid=9N438MZHD3ZF";

    public static string glyphButtonRecentsDefault = "\uE838";
    public static string glyphButtonRecentsCustom = "\uEC25";
    public static string glyphButtonRecentsHistory = "\uE81C";
    public static string glyphFormatImage = "\uEB9F";
    public static string glyphFormatPdf = "\uEA90";
}