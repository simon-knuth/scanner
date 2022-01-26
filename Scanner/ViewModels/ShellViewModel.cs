using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.Helpers;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using static Utilities;

namespace Scanner.ViewModels
{
    public class ShellViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        private readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        public readonly IScanResultService ScanResultService = Ioc.Default.GetService<IScanResultService>();
        public readonly IHelperService HelperService = Ioc.Default.GetService<IHelperService>();
        public readonly IScanService ScanService = Ioc.Default.GetService<IScanService>();

        public event EventHandler TutorialPageListRequested;
        public event EventHandler ChangelogRequested;
        public event EventHandler SetupRequested;
        public event EventHandler FeedbackDialogRequested;
        public event EventHandler UpdatedDialogRequested;
        public event EventHandler PreviewDialogRequested;
        public event EventHandler ScanMergeDialogRequested;
        public event EventHandler<List<StorageFile>> ShareFilesChanged;

        public AsyncRelayCommand ShowScanSaveLocationCommand;
        public RelayCommand StatusMessageDismissedCommand => new RelayCommand(StatusMessageDismissed);
        public RelayCommand ViewLoadedCommand;
        public RelayCommand DebugCrashCommand => new RelayCommand(Crash);
        public RelayCommand DebugTrackErrorCommand => new RelayCommand(DebugTrackError);
        public RelayCommand DebugBroadcastStatusMessageCommand => new RelayCommand(DebugBroadcastStatusMessage);
        public RelayCommand DebugShowTutorialPageListCommand => new RelayCommand(DebugShowTutorialPageList);
        public RelayCommand DebugShowSetupCommand => new RelayCommand(DebugShowSetup);
        public RelayCommand DebugShowUpdatedDialogCommand => new RelayCommand(ShowUpdatedDialog);
        public RelayCommand ShowDonateDialogCommand;
        public RelayCommand ShowChangelogCommand => new RelayCommand(ShowChangelog);
        public RelayCommand DebugShowFeedbackDialogCommand;
        public AsyncRelayCommand StoreRatingCommand;

        private TaskCompletionSource<bool> DisplayedViewChanged = new TaskCompletionSource<bool>();

        private ShellNavigationSelectableItem _DisplayedView;
        public ShellNavigationSelectableItem DisplayedView
        {
            get => _DisplayedView;
            set
            {
                LogService?.Log.Information($"DisplayedView = {value}");
                SetProperty(ref _DisplayedView, value, true);
                DisplayedViewChanged.TrySetResult(true);
            }
        }

        private bool? _IsDefaultSaveLocation;
        public bool? IsDefaultSaveLocation
        {
            get => _IsDefaultSaveLocation;
            set => SetProperty(ref _IsDefaultSaveLocation, value);
        }

        private ObservableCollection<AppWideStatusMessage> _StatusMessages = new ObservableCollection<AppWideStatusMessage>();
        public ObservableCollection<AppWideStatusMessage> StatusMessages
        {
            get => _StatusMessages;
            set => SetProperty(ref _StatusMessages, value);
        }

        private AppWideStatusMessage _SelectedStatusMessage;
        public AppWideStatusMessage SelectedStatusMessage
        {
            get => _SelectedStatusMessage;
            set => SetProperty(ref _SelectedStatusMessage, value);
        }

        private int _SelectedStatusMessageIndex;
        public int SelectedStatusMessageIndex
        {
            get => _SelectedStatusMessageIndex;
            set
            {
                try { SelectedStatusMessage = StatusMessages[value]; } catch { }
                SetProperty(ref _SelectedStatusMessageIndex, value);
            }
        }

        private string _TitlebarText;
        public string TitlebarText
        {
            get => _TitlebarText;
            set => SetProperty(ref _TitlebarText, value);
        }

        private string _NarratorStatusText;
        public string NarratorStatusText
        {
            get => _NarratorStatusText;
            set
            {
                SetProperty(ref _NarratorStatusText, "");
                SetProperty(ref _NarratorStatusText, value);
            }
        }

        private int _NumberOfPages;
        public int NumberOfPages
        {
            get => _NumberOfPages;
            set => SetProperty(ref _NumberOfPages, value);
        }

        private bool _ShowAnimations;
        public bool ShowAnimations
        {
            get => _ShowAnimations;
            set => SetProperty(ref _ShowAnimations, value);
        }

        private AppWideStatusMessage _DebugStatusMessage = new AppWideStatusMessage();
        public AppWideStatusMessage DebugStatusMessage
        {
            get => _DebugStatusMessage;
            set => SetProperty(ref _DebugStatusMessage, value);
        }

        public List<AppWideStatusMessageSeverity> DebugStatusMessageSeverities = new List<AppWideStatusMessageSeverity>()
        {
            AppWideStatusMessageSeverity.Error,
            AppWideStatusMessageSeverity.Informational,
            AppWideStatusMessageSeverity.Success,
            AppWideStatusMessageSeverity.Warning
        };


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ShellViewModel()
        {
            Messenger.Register<HelpRequestShellMessage>(this, (r, m) => DisplayHelpView(r, m));
            Messenger.Register<SettingsRequestShellMessage>(this, (r, m) => DisplaySettingsView(r, m));
            Messenger.Register<AppWideStatusMessage>(this, (r, m) => ReceiveAppWideMessage(r, m));
            Messenger.Register<EditorSelectionTitleChangedMessage>(this, (r, m) => RefreshAppTitle(m.Title));
            Messenger.Register<SetShareFilesMessage>(this, (r, m) => ShareFilesChanged?.Invoke(this, m.Files));
            Messenger.Register<DonateDialogRequestMessage>(this, (r, m) => DisplayedView = ShellNavigationSelectableItem.Donate);
            Messenger.Register<PreviewDialogRequestMessage>(this, (r, m) => PreviewDialogRequested?.Invoke(this, EventArgs.Empty));
            Messenger.Register<ScanMergeDialogRequestMessage>(this, (r, m) => ScanMergeDialogRequested?.Invoke(this, EventArgs.Empty));
            Messenger.Register<NarratorAnnouncementMessage>(this, (r, m) => RequestNarratorAnnouncement(m.AnnouncementText));
            Window.Current.Activated += Window_Activated;
            ShowScanSaveLocationCommand = new AsyncRelayCommand(ShowScanSaveLocation);
            ShowDonateDialogCommand = new RelayCommand(() => DisplayedView = ShellNavigationSelectableItem.Donate);
            DebugShowFeedbackDialogCommand = new RelayCommand(DebugShowFeedbackDialog);
            StoreRatingCommand = new AsyncRelayCommand(DisplayStoreRatingDialogAsync);
            ViewLoadedCommand = new RelayCommand(ViewLoaded);
            RefreshAnimationsSetting();

            SettingsService.ScanSaveLocationChanged += SettingsService_ScanSaveLocationChanged;
            SettingsService.SettingChanged += SettingsService_SettingChanged;
            IsDefaultSaveLocation = SettingsService.IsScanSaveLocationDefault;
            ScanResultService.ScanResultCreated += ScanResultService_ScanResultCreated;
            ScanResultService.ScanResultChanged += ScanResultService_ScanResultChanged;
            ScanResultService.ScanResultDismissed += ScanResultService_ScanResultDismissed;
            ScanService.ScanEnded += ScanService_ScanEnded;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async void DisplayHelpView(object r, HelpRequestShellMessage m)
        {
            DisplayedViewChanged = new TaskCompletionSource<bool>();

            // ensure that help is displayed and wait until it's ready
            DisplayedView = ShellNavigationSelectableItem.Help;
            await DisplayedViewChanged.Task;

            // relay requested topic
            var newRequest = new HelpRequestMessage(m.HelpTopic);
            Messenger.Send(newRequest);

            AppCenterService.TrackEvent(AppCenterEvent.HelpRequested, new Dictionary<string, string> {
                            { "Topic", m.HelpTopic.ToString() },
                        });
        }

        private async void DisplaySettingsView(object r, SettingsRequestShellMessage m)
        {
            DisplayedViewChanged = new TaskCompletionSource<bool>();

            // ensure that settings are displayed and wait until they're ready
            DisplayedView = ShellNavigationSelectableItem.Settings;
            await DisplayedViewChanged.Task;

            // relay requested section
            var newRequest = new SettingsRequestMessage(m.SettingsSection);
            Messenger.Send(newRequest);

            AppCenterService.TrackEvent(AppCenterEvent.SettingsRequested, new Dictionary<string, string> {
                            { "Section", m.SettingsSection.ToString() },
                        });
        }

        private void RefreshAnimationsSetting()
        {
            ShowAnimations = (bool)SettingsService.GetSetting(AppSetting.SettingAnimations);
        }

        private void SettingsService_SettingChanged(object sender, AppSetting e)
        {
            if (e == AppSetting.SettingAnimations)
            {
                RefreshAnimationsSetting();
            }
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            WindowActivationState = e.WindowActivationState;
        }

        private CoreWindowActivationState windowActivationState;
        public CoreWindowActivationState WindowActivationState
        {
            get => windowActivationState;
            set => SetProperty(ref windowActivationState, value, true);
        }

        private async Task ShowScanSaveLocation()
        {
            LogService?.Log.Information("ShowScanSaveLocation");
            await RunOnUIThreadAndWaitAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    if ((SettingSaveLocationType)SettingsService.GetSetting(AppSetting.SettingSaveLocationType)
                        == SettingSaveLocationType.SetLocation)
                    {

                        await Launcher.LaunchFolderAsync(SettingsService.ScanSaveLocation);
                    }
                    else
                    {
                        await Launcher.LaunchFolderPathAsync(SettingsService.LastSaveLocationPath);
                    }
                }
                catch (Exception exc)
                {
                    LogService?.Log.Error(exc, "Couldn't display save location.");
                }
            });
        }

        private void SettingsService_ScanSaveLocationChanged(object sender, EventArgs e)
        {
            IsDefaultSaveLocation = SettingsService.IsScanSaveLocationDefault;
        }

        private void ReceiveAppWideMessage(object r, AppWideStatusMessage m)
        {
            LogService?.Log.Information($"ReceiveAppWideMessage: {m.Title} | {m.MessageText} | {m.AdditionalText}");
            m.Title?.Trim();
            m.MessageText?.Trim();
            m.AdditionalText?.Trim();

            StatusMessages.Insert(0, m);
            SelectedStatusMessageIndex = 0;

            RequestNarratorAnnouncement(LocalizedString("TextNewStatusMessageAccessibility"));
        }

        private void StatusMessageDismissed()
        {
            StatusMessages.Remove(SelectedStatusMessage);
            SelectedStatusMessageIndex = SelectedStatusMessageIndex;
        }

        private void DebugBroadcastStatusMessage()
        {
            Messenger.Send(DebugStatusMessage);
            DebugStatusMessage = new AppWideStatusMessage();
        }

        private void RefreshAppTitle(string title)
        {
            TitlebarText = title;
            
            // currently not setting app title because this doesn't behave well with narrator enabled
            //ApplicationView view = ApplicationView.GetForCurrentView();
            //view.Title = TitlebarText;
        }

        private void ScanResultService_ScanResultChanged(object sender, EventArgs e)
        {
            if (ScanResultService.Result != null)
            {
                NumberOfPages = ScanResultService.Result.NumberOfPages;

                if (ScanResultService.Result.NumberOfPages >= 2)
                {
                    RequestTutorialPageListIfNeeded();
                }
            }
        }

        private void ScanResultService_ScanResultCreated(object sender, ScanResult e)
        {
            if (ScanResultService.Result != null)
            {
                NumberOfPages = ScanResultService.Result.NumberOfPages;

                if (ScanResultService.Result.NumberOfPages >= 2)
                {
                    RequestTutorialPageListIfNeeded();
                }
            }
        }

        private void RequestTutorialPageListIfNeeded()
        {
            if ((bool)SettingsService?.GetSetting(AppSetting.TutorialPageListShown) == false)
            {
                TutorialPageListRequested?.Invoke(this, EventArgs.Empty);
                SettingsService?.SetSetting(AppSetting.TutorialPageListShown, true);
            }
        }

        private void DebugShowTutorialPageList()
        {
            TutorialPageListRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ShowChangelog()
        {
            ChangelogRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ShowUpdatedDialog()
        {
            UpdatedDialogRequested?.Invoke(this, EventArgs.Empty);
        }

        private void DebugShowSetup()
        {
            SetupRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ScanResultService_ScanResultDismissed(object sender, EventArgs e)
        {
            RefreshAppTitle("");
            NumberOfPages = 0;
        }

        private void ViewLoaded()
        {
            if ((bool)SettingsService.GetSetting(AppSetting.SetupCompleted) == true
                && (bool)SettingsService.GetSetting(AppSetting.IsFirstAppLaunchEver) == false
                && (bool)SettingsService.GetSetting(AppSetting.IsFirstAppLaunchWithThisVersion) == true
                && SystemInformation.Instance.PreviousVersionInstalled.Major != 3
                && SystemInformation.Instance.PreviousVersionInstalled.Minor != 1)
            {
                UpdatedDialogRequested?.Invoke(this, EventArgs.Empty);
            }
            else if ((bool)SettingsService.GetSetting(AppSetting.SetupCompleted) == false)
            {
                SetupRequested?.Invoke(this, EventArgs.Empty);
            }

            if (SettingsService.IsSaveLocationUnavailable)
            {
                Messenger.Send(new AppWideStatusMessage
                {
                    Title = LocalizedString("ErrorMessageLoadScanFolderHeading"),
                    MessageText = LocalizedString("ErrorMessageLoadScanFolderBody"),
                    Severity = AppWideStatusMessageSeverity.Error
                });
            }
        }

        private void DebugShowFeedbackDialog()
        {
            FeedbackDialogRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task DisplayStoreRatingDialogAsync()
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await HelperService.ShowRatingDialogAsync();
            });
        }

        private void ScanService_ScanEnded(object sender, EventArgs e)
        {
            if (ScanService.CompletedScans == 10)
            {
                FeedbackDialogRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void RequestNarratorAnnouncement(string announcement)
        {
            NarratorStatusText = announcement;
        }

        private void Crash()
        {
            AppCenterService?.GenerateTestCrash();
        }

        private void DebugTrackError()
        {
            AppCenterService?.TrackError(new ApplicationException("Debug exception"));
        }
    }

    public enum ShellNavigationSelectableItem
    {
        ScanOptions,
        PageList,
        Editor,
        Help,
        Donate,
        Settings
    }
}
