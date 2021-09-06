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
using Windows.System;
using Windows.UI.Core;
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

        public AsyncRelayCommand ShowScanSaveLocationCommand;
        public RelayCommand StatusMessageDismissedCommand => new RelayCommand(StatusMessageDismissed);
        public RelayCommand DebugBroadcastStatusMessageCommand => new RelayCommand(DebugBroadcastStatusMessage);

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

        private ObservableCollection<AppWideMessage> _StatusMessages = new ObservableCollection<AppWideMessage>();
        public ObservableCollection<AppWideMessage> StatusMessages
        {
            get => _StatusMessages;
            set => SetProperty(ref _StatusMessages, value);
        }

        private AppWideMessage _SelectedStatusMessage;
        public AppWideMessage SelectedStatusMessage
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

        private AppWideMessage _DebugStatusMessage = new AppWideMessage();
        public AppWideMessage DebugStatusMessage
        {
            get => _DebugStatusMessage;
            set => SetProperty(ref _DebugStatusMessage, value);
        }

        public List<AppWideMessageSeverity> DebugStatusMessageSeverities = new List<AppWideMessageSeverity>()
        {
            AppWideMessageSeverity.Error,
            AppWideMessageSeverity.Informational,
            AppWideMessageSeverity.Success,
            AppWideMessageSeverity.Warning
        };

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ShellViewModel()
        {
            Messenger.Register<HelpRequestShellMessage>(this, (r, m) => DisplayHelpView(r, m));
            Messenger.Register<AppWideMessage>(this, (r, m) => ReceiveAppWideMessage(r, m));
            Window.Current.Activated += Window_Activated;
            ShowScanSaveLocationCommand = new AsyncRelayCommand(ShowScanSaveLocation);

            SettingsService.ScanSaveLocationChanged += SettingsService_ScanSaveLocationChanged;
            IsDefaultSaveLocation = SettingsService.IsScanSaveLocationDefault;
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

        private void ReceiveAppWideMessage(object r, AppWideMessage m)
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
            DebugStatusMessage = new AppWideMessage();
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
