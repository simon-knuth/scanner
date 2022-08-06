using System;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using static Utilities;

namespace Scanner.Views.Dialogs
{
    public sealed partial class SetupDialogView : ContentDialog
    {
        private SetupDialogStep currentStep;
        private bool closing;
        
        public SetupDialogView()
        {
            this.InitializeComponent();
            ViewModel.ErrorOccurred += ViewModel_ErrorOccurred;
        }

        private async void ViewModel_ErrorOccurred(object sender, Tuple<string, string> e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.High, async () =>
            {
                MessageDialog dialog = new MessageDialog(e.Item1, e.Item2);
                await dialog.ShowAsync();
            });
        }

        private void ContentDialog_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // load actual content later to fix transitions
            FindName("GridContent");
        }

        private void ContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (closing)
            {
                ViewModel.ConfirmSettingsCommand.Execute(null);
            }
            else
            {
                args.Cancel = true;
            }
        }

        private async void ButtonConfirm_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (currentStep == SetupDialogStep.Privacy)
                {
                    currentStep = SetupDialogStep.Saving;
                    SwitchPresenterContent.Value = "Saving";
                    ButtonBack.IsEnabled = true;
                }
                else if (currentStep == SetupDialogStep.Saving)
                {
                    closing = true;
                    this.Hide();
                }
            });
        }

        private async void ButtonBack_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (currentStep == SetupDialogStep.Saving)
                {
                    ButtonBack.IsEnabled = false;
                    currentStep = SetupDialogStep.Privacy;
                    SwitchPresenterContent.Value = "Privacy";
                }
            });
        }
    }

    public enum SetupDialogStep
    {
        Privacy,
        Saving,
    }
}
