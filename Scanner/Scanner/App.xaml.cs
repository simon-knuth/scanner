using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Scanner.AppWindows;
using Scanner.Services.Interfaces;
using Scanner.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Windows.AppLifecycle;
using Sentry;
using Microsoft.UI.Dispatching;
using Scanner.Extensions;

namespace Scanner
{
    public partial class App : Application
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private ILogService? LogService;
        #endregion

        #region Events
        public event EventHandler<KeyRoutedEventArgs> KeyDown;
        #endregion

        public MainWindow MainWindow;
        public SettingsWindow? SettingsWindow;
        public FeedbackWindow? FeedbackWindow;
        public DispatcherQueue MainDispatcherQueue;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public App()
        {
            this.InitializeComponent();

            // configure service landscape
            Ioc.Default.ConfigureServices(new ServiceCollection()
                .AddSingleton<IMessenger>(WeakReferenceMessenger.Default)
                .AddSingleton<ILogService, LogService>()
                .AddSingleton<IScannerDiscoveryService, ScannerDiscoveryService>()
                .AddSingleton<IProjectService, ProjectService>()
                .AddSingleton<IAppDataService, AppDataService>()
                .AddSingleton<ISettingsService, SettingsService>()
                .AddSingleton<ISentryService, SentryService>()
                .AddSingleton<ITesseractService, TesseractService>()
                .AddSingleton<ISaveLocationService, SaveLocationService>()
                .AddSingleton<ICopilotRuntimeService, CopilotRuntimeService>()
                .AddSingleton<IAccessibilityService, AccessibilityService>()
                .BuildServiceProvider());
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // get the activation args
            var appArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

            // get or register the main instance
            var mainInstance = AppInstance.FindOrRegisterForKey("main");
            MainDispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _ = Task.Run(async () =>
            {
                // if the main instance isn't this current instance
                if (!mainInstance.IsCurrent)
                {
                    // redirect activation to that instance
                    await mainInstance.RedirectActivationToAsync(appArgs);

                    // exit this instance and stop
                    Process.GetCurrentProcess().Kill();
                    return;
                }

                // register event handler
                UnhandledException += App_UnhandledException;

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
                    MainWindow.Closed += MainWindow_Closed;
                    MainWindow.Activate();
                });

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
            });
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (SettingsWindow != null)
            {
                SettingsWindow.Close();
            }
        }

        public void ShowSettings()
        {
            if (SettingsWindow == null)
            {
                SettingsWindow = new SettingsWindow();
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

        public void InvokeKeyDown(KeyRoutedEventArgs e)
        {
            KeyDown?.Invoke(this, e);
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogService?.Log.Fatal(e.Exception, "CRASH");
            LogService?.CloseAndFlush();

            //Ioc.Default.GetService<ISentryService>().TrackError(e.Exception, true);
            //SentrySdk.Flush();
        }
    }
}
