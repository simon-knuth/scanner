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
    public static ApplicationViewTitleBar applicationViewTitlebar;

    public static AppServiceConnection appServiceConnection;
    public static BackgroundTaskDeferral appServiceDeferral;
    public static TaskCompletionSource<bool> taskCompletionSource = null;
}