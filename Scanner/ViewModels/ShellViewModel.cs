using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
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
using static Scanner.Services.Messenger.MessengerEnums;


namespace Scanner.ViewModels
{
    public class ShellViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        public readonly IScanResultService ScanResultService = Ioc.Default.GetService<IScanResultService>();

        public event EventHandler TutorialPageListRequested;
        public event EventHandler<List<StorageFile>> ShareFilesChanged;

        public AsyncRelayCommand ShowScanSaveLocationCommand;
        public RelayCommand StatusMessageDismissedCommand => new RelayCommand(StatusMessageDismissed);
        public RelayCommand DebugBroadcastStatusMessageCommand => new RelayCommand(DebugBroadcastStatusMessage);
        public RelayCommand DebugShowTutorialPageListCommand => new RelayCommand(DebugShowTutorialPageList);

        private TaskCompletionSource<bool> DisplayedViewChanged = new TaskCompletionSource<bool>();

        private ShellNavigationSelectableItem _DisplayedView;
        public ShellNavigationSelectableItem DisplayedView
        {
            get => _DisplayedView;
            set
            {
                SetProperty(ref _DisplayedView, value, true);
                DisplayedViewChanged.TrySetResult(true);
            }
        }

        private bool _IsDefaultSaveLocation;
        public bool IsDefaultSaveLocation
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
            Messenger.Register<AppWideStatusMessage>(this, (r, m) => ReceiveAppWideMessage(r, m));
            Messenger.Register<EditorSelectionTitleChangedMessage>(this, (r, m) => RefreshAppTitle(m.Title));
            Messenger.Register<SetShareFilesMessage>(this, (r, m) => ShareFilesChanged?.Invoke(this, m.Files));
            Window.Current.Activated += Window_Activated;
            ShowScanSaveLocationCommand = new AsyncRelayCommand(ShowScanSaveLocation);

            SettingsService.ScanSaveLocationChanged += SettingsService_ScanSaveLocationChanged;
            IsDefaultSaveLocation = SettingsService.IsScanSaveLocationDefault;
            ScanResultService.ScanResultCreated += ScanResultService_ScanResultCreated;
            ScanResultService.ScanResultChanged += ScanResultService_ScanResultChanged;
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
            try
            {
                await Launcher.LaunchFolderAsync(SettingsService.ScanSaveLocation);
            }
            catch (Exception exc)
            {
                LogService?.Log.Error(exc, "Couldn't display save location.");
            }
        }

        private void SettingsService_ScanSaveLocationChanged(object sender, EventArgs e)
        {
            IsDefaultSaveLocation = SettingsService.IsScanSaveLocationDefault;
        }

        private void ReceiveAppWideMessage(object r, AppWideStatusMessage m)
        {
            m.Title?.Trim();
            m.MessageText?.Trim();
            m.AdditionalText?.Trim();

            StatusMessages.Insert(0, m);
            SelectedStatusMessageIndex = 0;
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
            
            ApplicationView view = ApplicationView.GetForCurrentView();
            view.Title = TitlebarText;
        }

        private void ScanResultService_ScanResultChanged(object sender, EventArgs e)
        {
            if (ScanResultService.Result.NumberOfPages >= 2)
            {
                RequestTutorialPageListIfNeeded();
            }
        }

        private void ScanResultService_ScanResultCreated(object sender, ScanResult e)
        {
            if (ScanResultService.Result.NumberOfPages >= 2)
            {
                RequestTutorialPageListIfNeeded();
            }
        }

        private void RequestTutorialPageListIfNeeded()
        {
            if ((bool)SettingsService?.GetSetting(SettingsEnums.AppSetting.TutorialPageListShown) == false)
            {
                TutorialPageListRequested?.Invoke(this, EventArgs.Empty);
                SettingsService?.SetSetting(SettingsEnums.AppSetting.TutorialPageListShown, true);
            }
        }

        private void DebugShowTutorialPageList()
        {
            TutorialPageListRequested?.Invoke(this, EventArgs.Empty);
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
