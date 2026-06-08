using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Services.Interfaces;
using Scanner.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace Scanner.Views.Settings;

[ObservableObjectAttribute]
public sealed partial class SettingsViewGeneral : SettingsPage
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public SettingsViewModel? ViewModel;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public SettingsViewGeneral()
    {
        this.InitializeComponent();
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        ViewModel = e.Parameter as SettingsViewModel;
    }

    private void Page_Loading(FrameworkElement sender, object args)
    {
        if (ViewModel == null) return;
        ViewModel.ViewLoadingCommand.Execute(this.DispatcherQueue);
    }

    private void SettingsCardEditCustomFileNamingPattern_Click(object sender, RoutedEventArgs e)
    {
        OnPageNavigationRequested(typeof(SettingsViewCustomItemNaming), CustomItemNamingViewModel.ItemNamingKind.File);
    }

    private void SettingsCardEditCustomSubfolderNamingPattern_Click(object sender, RoutedEventArgs e)
    {
        OnPageNavigationRequested(typeof(SettingsViewCustomItemNaming), CustomItemNamingViewModel.ItemNamingKind.Folder);
    }

    private void ToggleSwitchGenerateFileNameWithAI_Toggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        if (ToggleSwitchGenerateFileNameWithAI.IsOn && !ViewModel.CopilotRuntimeService.AreModelsInstalled)
        {
            ToggleSwitchGenerateFileNameWithAI.IsOn = false;

            TeachingTipCopilotRuntimeDownload.Target = ToggleSwitchGenerateFileNameWithAI;
            TeachingTipCopilotRuntimeDownload.IsOpen = true;
        }
    }

    private void HyperlinkButtonAIDisclaimer_Click(object sender, RoutedEventArgs e)
    {
        TeachingTipAIDisclaimer.Target = HyperlinkButtonAIDisclaimer;
        TeachingTipAIDisclaimer.IsOpen = true;
    }
}
