using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle;
using Scanner.AppWindows;
using Scanner.Extensions;
using Scanner.Helpers;
using Scanner.Messages;
using Scanner.Services;
using Scanner.Services.Interfaces;
using Scanner.ViewModels;
using Sentry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Scanner;

public partial class App : Application
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private ILogService? LogService;
    #endregion

    public MainWindow MainWindow;
    public SettingsWindow? SettingsWindow;
    public FeedbackWindow? FeedbackWindow;
    public DispatcherQueue MainDispatcherQueue;

    public bool _launched;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public App()
    {
        this.InitializeComponent();

        // register error event handlers
        UnhandledException += App_UnhandledException;
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LogService?.Log.Fatal(e.Exception, "Unobserved task exception");

            if (LogService == null)
                FallbackFatalLogging(e.Exception);

            e.SetObserved();
        };

        // configure service landscape
        Ioc.Default.ConfigureServices(new ServiceCollection()
            .AddSingleton<IMessenger>(WeakReferenceMessenger.Default)
            .AddSingleton<ILogService, LogService>()
            .AddSingleton<IScannerDiscoveryService, ScannerDiscoveryService>()
            .AddSingleton<IProjectService, ProjectService>()
            .AddSingleton<IAppDataService, AppDataService>()
            .AddSingleton<ISettingsService, SettingsService>()
            .AddSingleton<ISentryService, SentryService>()
            .AddSingleton<IOcrService, OcrService>()
            .AddSingleton<ISaveLocationService, SaveLocationService>()
            .AddSingleton<ICopilotRuntimeService, CopilotRuntimeService>()
            .AddSingleton<IAccessibilityService, AccessibilityService>()
            .AddSingleton<IProjectHistoryService, ProjectHistoryService>()
            .AddSingleton<IKnownScannersService, KnownScannersService>()
            .AddSingleton<ITemplatesService, TemplatesService>()
            .BuildServiceProvider());

        WeakReferenceMessenger.Default.Register<MainWindowClosingMessage>(this, (r, m) =>
        {
            SettingsWindow?.Close();
            FeedbackWindow?.Close();
            KeyboardHookHelper.Unhook();
        });
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // get the activation args
        var appArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

        // get or register the main instance
        var mainInstance = AppInstance.FindOrRegisterForKey("main");
        MainDispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _ = Task.Run(async () =>
        {
            try
            {
                // if the main instance isn't this current instance
                if (mainInstance.ProcessId != Environment.ProcessId)
                {
                    // redirect activation to that instance
                    await mainInstance.RedirectActivationToAsync(appArgs);

                    // exit this instance and stop
                    Process.GetCurrentProcess().Kill();
                    return;
                }

                if (!_launched)
                {
                    // initialize essential singleton services
                    LogService = Ioc.Default.GetService<ILogService>();
                    if (LogService != null)
                    {
                        await LogService.InitializeAsync();
                    }
                    Ioc.Default.GetService<ISentryService>()?.Initialize();
                    await Ioc.Default.GetRequiredService<IAppDataService>().InitializeAsync();
                    Ioc.Default.GetRequiredService<ISaveLocationService>();

                    MainDispatcherQueue.RunOnThread(DispatcherQueuePriority.High, () =>
                    {
                        MainWindow = new MainWindow();
                        MainWindow.Activate();
                        KeyboardHookHelper.Initialize(MainWindow);
                        _ = Task.Run(async () => await Ioc.Default.GetRequiredService<IKnownScannersService>().InitializeAsync());
                        _ = Task.Run(async () => await Ioc.Default.GetRequiredService<IProjectHistoryService>().InitializeAsync(MainDispatcherQueue));
                        _ = Task.Run(async () => await Ioc.Default.GetRequiredService<ITemplatesService>().InitializeAsync(MainDispatcherQueue));
                    });
                }
                else
                {
                    MainDispatcherQueue.RunOnThread(DispatcherQueuePriority.High, () =>
                    {
                        MainWindow?.Activate();
                    });
                }

                _launched = true;

                //try
                //{
                //    // analytics
                //    IActivatedEventArgs activatedEventArgs = Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
                //    if (activatedEventArgs != null)
                //    {
                //        Dictionary<string, string> properties = new Dictionary<string, string>();
                //        switch (activatedEventArgs.Kind)
                //        {
                //            case ActivationKind.Launch:
                //            default:
                //                properties.Add("Kind", "Launch");
                //                break;
                //            case ActivationKind.StartupTask:
                //                properties.Add("Kind", "StartupTask");
                //                break;
                //        }
                //        Ioc.Default.GetService<ISentryService>()?.TrackEvent(AnalyticsEvent.Launch, properties);
                //    }
                //}
                //catch (Exception exc)
                //{
                //    Ioc.Default.GetService<ISentryService>()?.TrackError(exc);
                //}
            }
            catch (Exception exc)
            {
                LogService?.Log.Fatal(exc, "Unhandled background exception");
                LogService?.CloseAndFlush();
            }
            
        });
    }

    public void ShowSettings(SettingsViewModelIntent? intent)
    {
        if (SettingsWindow == null)
        {
            SettingsWindow = new SettingsWindow(intent);
            SettingsWindow.Closed += SettingsWindow_Closed;
        }
        SettingsWindow.Activate();
    }

    public void ShowFeedback()
    {
        if (FeedbackWindow == null)
        {
            FeedbackWindow = new FeedbackWindow();
            FeedbackWindow.Closed += FeedbackWindow_Closed;
        }
        FeedbackWindow.Activate();
    }

    private void SettingsWindow_Closed(object sender, WindowEventArgs args)
    {
        SettingsWindow = null;
    }

    private void FeedbackWindow_Closed(object sender, WindowEventArgs args)
    {
        FeedbackWindow = null;
    }


    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogService?.Log.Fatal(e.Exception, "CRASH");
        LogService?.CloseAndFlush();

        if (LogService == null)
            FallbackFatalLogging(e.Exception);

        Ioc.Default.GetService<ISentryService>()?.TrackError(e.Exception, true);
        SentrySdk.Flush();
    }

    private void FallbackFatalLogging(Exception exc)
    {
        try
        {
            string tempPath = Windows.Storage.ApplicationData.Current.TemporaryFolder.Path;
            string fileName = $"crash.txt";
            string filePath = Path.Combine(tempPath, fileName);

            string content = $"""
                CRASH at {DateTime.Now:O}

                {exc}
                """;

            File.WriteAllText(filePath, content);
        }
        catch
        {
            // Nothing we can do if writing the fallback log also fails.
        }
    }
}
