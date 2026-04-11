using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Scanner.Resources;
using Scanner.Services.Interfaces;
using Sentry;
using Serilog;
using Serilog.Sinks.File;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Storage;
using Windows.System;
using WinRT.Interop;

namespace Scanner.Services;

internal class SentryService : ISentryService
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
    #endregion

    private readonly string DataSourceName = Secrets.SENTRY_DSN != "SENTRY_DSN_GOES_HERE" ? Secrets.SENTRY_DSN : "";

    public bool HasConsent
    {
        get;
        private set;
    }

    public string UserId
    {
        get;
        private set;
    }

    private Action<Scope> defaultScope;
    private Action<Scope> errorScope;
    private Action<Scope> warningScope;
    private Action<Scope> errorFeedbackScope;
    private Action<Scope> suggestionFeedbackScope;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public SentryService()
    {
        SettingsService.PropertyChanged += SettingsService_PropertyChanged;
        UserId = SettingsService.UserId;
        if (LogService != null)
        {
            LogService.LogFilePathChanged += LogService_LogFilePathChanged;
        }
        SettingsService.DiagnosticEventsSentThisSession = 0;

        defaultScope = (scope) =>
        {
            scope.User.Id = UserId;
            scope.User.IpAddress = null;
            scope.Level = SentryLevel.Fatal;

            // attachment
            scope.ClearAttachments();
            if (new Random().NextDouble() <= AppConfig.CrashAttachmentRate)
            {
                FileAttachmentContent fileAttachment = new FileAttachmentContent(LogService?.LogFilePath);
                SentryAttachment attachment = new SentryAttachment(AttachmentType.Default, fileAttachment, "log.log", "application/log");
                scope.AddAttachment(attachment);
            }
        };

        errorScope = (scope) =>
        {
            scope.Level = SentryLevel.Error;

            // attachment
            scope.ClearAttachments();
            if (new Random().NextDouble() <= AppConfig.ErrorAttachmentRate)
            {
                FileAttachmentContent fileAttachment = new FileAttachmentContent(LogService?.LogFilePath);
                SentryAttachment attachment = new SentryAttachment(AttachmentType.Default, fileAttachment, "log.log", "application/log");
                scope.AddAttachment(attachment);
            }
        };

        warningScope = (scope) =>
        {
            scope.Level = SentryLevel.Warning;

            // attachment
            scope.ClearAttachments();
            if (new Random().NextDouble() <= AppConfig.WarningAttachmentRate)
            {
                FileAttachmentContent fileAttachment = new FileAttachmentContent(LogService?.LogFilePath);
                SentryAttachment attachment = new SentryAttachment(AttachmentType.Default, fileAttachment, "log.log", "application/log");
                scope.AddAttachment(attachment);
            }
        };

        errorFeedbackScope = (scope) =>
        {
            scope.Level = SentryLevel.Info;

            // attachment
            scope.ClearAttachments();
            if (new Random().NextDouble() <= AppConfig.WarningAttachmentRate)
            {
                FileAttachmentContent fileAttachment = new FileAttachmentContent(LogService?.LogFilePath);
                SentryAttachment attachment = new SentryAttachment(AttachmentType.Default, fileAttachment, "log.log", "application/log");
                scope.AddAttachment(attachment);
            }
        };

        suggestionFeedbackScope = (scope) =>
        {
            scope.Level = SentryLevel.Info;

            // attachment
            scope.ClearAttachments();
        };
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    ///     Intialize the Sentry connection.
    /// </summary>
    public void Initialize()
    {
        HasConsent = SettingsService.SettingErrorStatistics;

        // build options
        SentryOptions options = new SentryOptions
        {
            Dsn = HasConsent ? DataSourceName : string.Empty,
            IsGlobalModeEnabled = true,
            AutoSessionTracking = true,
            Release = Helpers.Helpers.GetCurrentVersion(),
            SendDefaultPii = false
        };

        options.SetBeforeSend((sentryEvent, hint) =>
        {
            // check limit
            int eventsSentThisSession = SettingsService.DiagnosticEventsSentThisSession;
            if (eventsSentThisSession >= AppConfig.MaxDiagnosticEventsPerSession)
            {
                // limit reached
                return null;
            }

            // get rate
            double rate = AppConfig.DefaultRate;
            switch (sentryEvent.Level)
            {
                case SentryLevel.Warning:
                    rate = AppConfig.WarningRate;
                    break;
                case SentryLevel.Error:
                    rate = AppConfig.ErrorRate;
                    break;
                case SentryLevel.Fatal:
                    rate = AppConfig.CrashRate;
                    break;
            }

            // apply rate
            if (new Random().NextDouble() > rate)
            {
                return null;
            }

            // accept event
            SettingsService.DiagnosticEventsSentThisSession = eventsSentThisSession + 1;
            return sentryEvent;
        });

#if DEBUG
        options.Environment = "debug";
#else
        options.Environment = "release";
#endif

        // initialize with options
        SentrySdk.Init(options);
        SentrySdk.ConfigureScope(defaultScope);
    }

    private void LogService_LogFilePathChanged(object? sender, string e)
    {
        SentrySdk.ConfigureScope(defaultScope);
    }

    /// <summary>
    ///     Refreshes the status when the user toggles analytics on or off.
    /// </summary>
    private void SettingsService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsService.SettingErrorStatistics):
                Initialize();
                break;
        }
    }


    /// <summary>
    ///     Returns an <see cref="ErrorAttachmentLog"/> that includes the relevant log file
    ///     for the given <paramref name="report"/>. If no report is specified, the newest log file is used.
    /// </summary>
    public async Task<string> GetCurrentLogPathAsync(bool flush)
    {
        // check whether LogService is available
        if (LogService == null)
        {
            return null;
        }

        // attempt to find log
        try
        {
            if (flush)
            {
                // close log file
                LogService.CloseAndFlush();
                await LogService.InitializeAsync();
            }

            // get all logs
            IReadOnlyList<StorageFile> files = await LogService.LogFolder.GetFilesAsync();

            // find relevant log
            List<StorageFile> sortedLogs = new List<StorageFile>(files);
            sortedLogs.Sort(delegate (StorageFile x, StorageFile y)
            {
                return DateTimeOffset.Compare(x.DateCreated, y.DateCreated);
            });
            sortedLogs.Reverse();

            // just take newest log
            return sortedLogs[0].Path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void TrackEvent(AnalyticsEvent sentryEvent, IDictionary<string, string> properties = null)
    {
        // add breadcrumb
        Breadcrumb breadcrumb = null;
        if (properties != null)
        {
            breadcrumb = new Breadcrumb(
            message: sentryEvent.ToString(),
            type: "event",
            category: "event",
            data: new ReadOnlyDictionary<string, string>(properties),
            level: BreadcrumbLevel.Info);
        }
        else
        {
            breadcrumb = new Breadcrumb(
            message: sentryEvent.ToString(),
            type: "event",
            category: "event",
            level: BreadcrumbLevel.Info);
        }
        SentrySdk.AddBreadcrumb(breadcrumb);

        string dictToText = "";
        if (properties != null)
        {
            dictToText = string.Join(",", properties.Select(pair =>
            {
                string key = pair.Key.ToString();
                string value = pair.Value == null ? "null" : pair.Value.ToString();
                return string.Format("{0}={1}", key, value);
            }).ToArray());
        }
        LogService?.Log.Information("Tracking {Event} with {Properties}", sentryEvent, dictToText);
    }

    public void TrackError(Exception exception, bool isFatal = false)
    {
        LogService?.Log.Information("Tracking error");
        if (isFatal)
        {
            SentrySdk.CaptureException(exception, defaultScope);
        }
        else
        {
            SentrySdk.CaptureException(exception, errorScope);
        }
    }

    public void TrackWarning(Exception exception)
    {
        LogService?.Log.Information("Tracking warning");
        SentrySdk.CaptureException(exception, warningScope);
    }

    public void GenerateTestCrash()
    {
#if DEBUG
        SentrySdk.CauseCrash(CrashType.Managed);
#endif
    }

    public void SendSuggestionFeedback(string message, string? contactEmail, string? name)
    {
        LogService?.Log.Information("Tracking suggestion feedback");
        SentryFeedback feedback = new(message, contactEmail, name);
        SentrySdk.CaptureFeedback(feedback, suggestionFeedbackScope);
    }

    public void SendErrorFeedback(string message, string? contactEmail, string? name)
    {
        LogService?.Log.Information("Tracking error feedback");
        SentryFeedback feedback = new(message, contactEmail, name);
        SentrySdk.CaptureFeedback(feedback, errorFeedbackScope);
    }
}
