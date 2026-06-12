using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Extensions;
using Scanner.Models;
using Scanner.Models.Interfaces;
using Scanner.Services.Interfaces;
using Scanner.ViewModels;
using Scanner.Views.TeachingTips;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization.NumberFormatting;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;
using static Scanner.Helpers.Helpers;


namespace Scanner.Views.Dialogs;

public partial class ScanMergeDialogView : ContentDialog
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
        Ioc.Default.GetService<ILogService>()?.Log.Information("Dialog loaded");

        ViewModel.CloseRequested += ViewModel_CloseRequested;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private async void ViewModel_CloseRequested(object sender, EventArgs e)
    {
        await this.RunOnUIThreadAndWaitAsync(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
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

    private void NumberBox_Loaded(object sender, RoutedEventArgs e)
    {
        NumberBox? numberBox = sender as NumberBox;
        if (numberBox == null)
            return;

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
