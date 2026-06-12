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
                    sentryService?.TrackEvent(AnalyticsEvent.Launch);
                    sentryService?.TrackEvent(AnalyticsEvent.ArchitectureDetected, new Dictionary<string, string>
                    {
                        { "architecture", System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString() }
                    });
                    TrackSettingsStats(sentryService);
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
                LogService?.Log.Fatal(exc, "Unhandled background exception");
                LogService?.CloseAndFlush();
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
