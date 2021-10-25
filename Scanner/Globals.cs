using Serilog;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;

public static class Globals
{
    public static ILogger log;

    public static StorageFolder scanFolder = null;
    public static ApplicationViewTitleBar applicationViewTitlebar;

    public static bool possiblyDeadScanner = false;

    public static int scanNumber;

    public static AppServiceConnection appServiceConnection;
    public static BackgroundTaskDeferral appServiceDeferral;
    public static TaskCompletionSource<bool> taskCompletionSource = null;

    public static SpeechSynthesizer narratorSpeech = new SpeechSynthesizer();
    public static MediaElement narratorMediaElement = new MediaElement();
}