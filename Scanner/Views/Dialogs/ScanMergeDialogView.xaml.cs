using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Globalization.NumberFormatting;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;

using static Utilities;

namespace Scanner.Views.Dialogs
{
    public sealed partial class ScanMergeDialogView : ContentDialog
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanMergeDialogView()
        {
            this.InitializeComponent();

            ViewModel.CloseRequested += ViewModel_CloseRequested;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private async void ViewModel_CloseRequested(object sender, EventArgs e)
        {
            await RunOnUIThreadAsync(CoreDispatcherPriority.Normal, () =>
            {
                this.Hide();
            });
        }

        private void ContentDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (args.Result == ContentDialogResult.Primary)
            {
                ViewModel.AcceptCommand.Execute(null);
            }
            else
            {
                ViewModel.CancelCommand.Execute(null);
            }
        }

        private void NumberBox_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            NumberBox numberBox = sender as NumberBox;

            // define rounding
            IncrementNumberRounder numberRounder = new IncrementNumberRounder
            {
                Increment = 1
            };

            // define formatting
            DecimalFormatter formatter = new DecimalFormatter
            {
                IntegerDigits = 1,
                FractionDigits = 0,
                IsGrouped = false,
                NumberRounder = numberRounder
            };
            numberBox.NumberFormatter = formatter;
        }
    }
}
