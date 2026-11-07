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
using Microsoft.Windows.Globalization;
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

        switch (SettingsService.ReadSettingAppTheme())
        {
            case SettingAppTheme.Light:
                RequestedTheme = ApplicationTheme.Light;
                break;
            case SettingAppTheme.Dark:
                RequestedTheme = ApplicationTheme.Dark;
                break;
        }

        // register error event handlers
        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            var log = LogService?.Log;
            if (log != null)
                log.Fatal(e.Exception, "Unobserved task exception");
            else
                FallbackFatalLogging(e.Exception, "Unobserved task exception");

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
            LogService?.Log.Information("Main window closing, shutting down");
            Ioc.Default.GetService<ISentryService>()?.TrackEvent(AnalyticsEvent.Close);
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
                    ISentryService? sentryService = Ioc.Default.GetService<ISentryService>();
                    sentryService?.Initialize();
                    ReportFallbackCrashLog(sentryService);
                    sentryService?.TrackEvent(AnalyticsEvent.Launch);
                    sentryService?.TrackEvent(AnalyticsEvent.ArchitectureDetected, new Dictionary<string, string>
                    {
                        { "architecture", System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString() }
                    });
                    TrackSettingsStats(sentryService);

                    // set app language
                    ISettingsService settingsService = Ioc.Default.GetRequiredService<ISettingsService>();
                    string languageTag = settingsService.SettingAppLanguage;
                    if (languageTag == "SYSTEM")
                    {
                        // no custom language selected
                        if (!string.IsNullOrEmpty(ApplicationLanguages.PrimaryLanguageOverride))
                            ApplicationLanguages.PrimaryLanguageOverride = string.Empty;    // This does not work atm, see https://github.com/haonanttt/WindowsAppSDK/issues/17
                    }
                    else
                    {
                        // custom language selected
                        if (ApplicationLanguages.PrimaryLanguageOverride != languageTag)
                            ApplicationLanguages.PrimaryLanguageOverride = languageTag;
                    }

                    await Ioc.Default.GetRequiredService<IAppDataService>().InitializeAsync();
                    Ioc.Default.GetRequiredService<ISaveLocationService>();

                    DateTime processStartTime = Process.GetCurrentProcess().StartTime;
                    MainDispatcherQueue.RunOnThread(DispatcherQueuePriority.High, () =>
                    {
                        MainWindow = new MainWindow();
                        MainWindow.Activate();
                        LogService?.Log.Information("Main window shown");
                        KeyboardHookHelper.Initialize(MainWindow);

                        // cold-start time (process creation to first window shown)
                        sentryService?.TrackDistributionMetric(AnalyticsMetric.AppColdStartDuration,
                            (DateTime.Now - processStartTime).TotalMilliseconds, MeasurementUnit.Duration.Millisecond);

                        _ = Task.Run(async () =>
                        {
                            IKnownScannersService knownScannersService = Ioc.Default.GetRequiredService<IKnownScannersService>();
                            await knownScannersService.InitializeAsync();
                            var knownScanners = await knownScannersService.GetKnownScannersAsync();
                            sentryService?.TrackGaugeMetric(AnalyticsMetric.KnownScannerCount, knownScanners.Count, MeasurementUnit.None);
                        });
                        _ = Task.Run(async () => await Ioc.Default.GetRequiredService<IProjectHistoryService>().InitializeAsync(MainDispatcherQueue));
                        _ = Task.Run(async () =>
                        {
                            ITemplatesService templatesService = Ioc.Default.GetRequiredService<ITemplatesService>();
                            await templatesService.InitializeAsync(MainDispatcherQueue);
                            sentryService?.TrackGaugeMetric(AnalyticsMetric.TemplateCount, templatesService.Entries.Count, MeasurementUnit.None);
                        });
                    });
                }
                else
                {
                    LogService?.Log.Information("Reactivating the existing instance");
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
                var log = LogService?.Log;
                if (log != null)
                {
                    log.Fatal(exc, "Unhandled background exception");
                    LogService!.CloseAndFlush();
                }
                else
                {
                    FallbackFatalLogging(exc, "Unhandled background exception");
                }
            }

        });
    }

    /// <summary>
    ///     Emits a snapshot of the user's notable settings, so their distribution across the user base can be analyzed.
    /// </summary>
    private static void TrackSettingsStats(ISentryService? sentryService)
    {
        if (sentryService == null) return;

        ISettingsService settings = Ioc.Default.GetRequiredService<ISettingsService>();
        sentryService.TrackEvent(AnalyticsEvent.SettingsStats, new Dictionary<string, string>
        {
            { "save_location_type", settings.SettingSaveLocationType.ToString() },
            { "theme", settings.SettingAppTheme.ToString() },
            { "auto_rotate", settings.SettingAutoRotate.ToString() },
            { "auto_save", settings.SettingAutoSave.ToString() },
            { "ai_file_name", settings.SettingGenerateFileNameWithAI.ToString() },
            { "ocr_pdfs", settings.SettingOcrPdfs.ToString() },
            { "use_sub_folder", settings.SettingUseSubfolder.ToString() },
            { "file_naming_pattern", settings.SettingFileNamingPattern.ToString() },
            { "sub_folder_naming_pattern", settings.SettingSubfolderNamingPattern.ToString() },
            { "measurement_units", settings.SettingMeasurementUnits.ToString() },
            { "scan_action", settings.SettingScanAction.ToString() },
            { "editor_orientation", settings.SettingEditorOrientation.ToString() },
            { "remember_scan_options", settings.SettingRememberScanOptions.ToString() },
            { "animations", settings.SettingAnimations.ToString() },
            { "mirror_layout", settings.SettingMirrorAppLayout.ToString() },
            { "error_statistics", settings.SettingErrorStatistics.ToString() },
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
        // For native errors e.Exception is a generic COMException, only e.Message carries WinUI's actual error text.
        var log = LogService?.Log;
        if (log != null)
        {
            log.Fatal(e.Exception, "CRASH: {Message}", e.Message);
            LogService!.CloseAndFlush();
        }
        else
        {
            FallbackFatalLogging(e.Exception, e.Message);
        }

        e.Exception.Data["WinUIMessage"] = e.Message;

        // Ioc.Default throws when the crash happens before the services are configured.
        try
        {
            Ioc.Default.GetService<ISentryService>()?.TrackError(e.Exception, true);
            SentrySdk.Flush();
        }
        catch
        {
            // The fallback crash log covers this case.
        }
    }

    /// <summary>
    /// Catches exceptions that bring down the process without passing through <see cref="App_UnhandledException"/>,
    /// e.g. ones thrown on thread pool threads by async void methods. Sentry captures these on its own.
    /// </summary>
    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        Exception exc = e.ExceptionObject as Exception ?? new Exception($"Non-exception object thrown: {e.ExceptionObject}");

        var log = LogService?.Log;
        if (log != null)
        {
            log.Fatal(exc, "CRASH (non-UI thread)");
            LogService!.CloseAndFlush();
        }
        else
        {
            FallbackFatalLogging(exc);
        }
    }

    private static string FallbackCrashLogPath => Path.Combine(Windows.Storage.ApplicationData.Current.TemporaryFolder.Path, "crash.txt");

    /// <summary>
    /// Records an error while the log isn't initialized yet. The next launch reports the file, see <see cref="ReportFallbackCrashLog"/>.
    /// </summary>
    private static void FallbackFatalLogging(Exception exc, string? message = null)
    {
        try
        {
            string heading = message != null ? $"CRASH at {DateTime.Now:O}: {message}" : $"CRASH at {DateTime.Now:O}";
            File.AppendAllText(FallbackCrashLogPath, $"{heading}\n{exc}\n\n");
        }
        catch
        {
            // Nothing we can do if writing the fallback log also fails.
        }
    }

    /// <summary>
    /// Sends errors that <see cref="FallbackFatalLogging"/> recorded during an earlier launch to the log and Sentry.
    /// </summary>
    private void ReportFallbackCrashLog(ISentryService? sentryService)
    {
        try
        {
            if (!File.Exists(FallbackCrashLogPath)) return;

            // Delete before reporting, so a report that fails can't repeat on every launch.
            string report = File.ReadAllText(FallbackCrashLogPath);
            File.Delete(FallbackCrashLogPath);

            LogService?.Log?.Warning("Previous launch crashed before logging was available: {Report}", report);
            sentryService?.TrackCrashReport(report);
        }
        catch (Exception exc)
        {
            LogService?.Log?.Error(exc, "Failed to report fallback crash log");
        }
    }
}
