using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace Scanner.ViewModels
{
    public class ShellViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();
        private readonly ILogService LogService = Ioc.Default.GetService<ILogService>();

        public AsyncRelayCommand ShowScanSaveLocationCommand;

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

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ShellViewModel()
        {
            Messenger.Register<HelpRequestShellMessage>(this, (r, m) => DisplayHelpView(r, m));
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
                LogService?.Error(exc, "Couldn't display save location.");
            }
        }

        private void SettingsService_ScanSaveLocationChanged(object sender, EventArgs e)
        {
            IsDefaultSaveLocation = SettingsService.IsScanSaveLocationDefault;
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
