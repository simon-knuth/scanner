using System;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using static Utilities;

namespace Scanner.Views.Dialogs
{
    public sealed partial class LicensesDialogView : ContentDialog
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public LicensesDialogView()
        {
            this.InitializeComponent();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async void HyperlinkButtonSettingsAboutLicenses_Click(object sender, RoutedEventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, async () =>
            {
                ButtonDialogLicensesHeadingBack.IsEnabled = false;

                if (FrameDialogLicenses.Content == null)
                {
                    FrameDialogLicenses.Navigate(typeof(LicensesView), new SuppressNavigationTransitionInfo());
                }
                if (FrameDialogLicenses.Content.GetType() == typeof(LicenseDetailView))
                {
                    FrameDialogLicenses.GoBack(new SuppressNavigationTransitionInfo());
                }
            });
        }
        private void ButtonDialogLicensesHeadingBack_Click(object sender, RoutedEventArgs e)
        {
            if (FrameDialogLicenses.Content.GetType() == typeof(LicenseDetailView))
            {
                FrameDialogLicenses.GoBack(new SlideNavigationTransitionInfo()
                { Effect = SlideNavigationTransitionEffect.FromRight });
            }
        }
        private void FrameDialogLicenses_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.SourcePageType == typeof(LicenseDetailView))
            {
                ButtonDialogLicensesHeadingBack.IsEnabled = true;
            }
            else
            {
                ButtonDialogLicensesHeadingBack.IsEnabled = false;
            }
        }

        private void ContentDialogRoot_Loaded(object sender, RoutedEventArgs e)
        {
            FrameDialogLicenses.Navigate(typeof(LicensesView), new SuppressNavigationTransitionInfo());
        }
    }
}
