using CommunityToolkit.Mvvm.DependencyInjection;
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
using Windows.System;


namespace Scanner.Views.Settings;

public sealed partial class SettingsViewTranslations : SettingsPage
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public SettingsViewModel? ViewModel;

    private static readonly string[] contributorNames =
    [
        "00000051",
        "1055773545",
        "Abdulkareem Morshid",
        "Abdullah Ishtiwy",
        "Almas",
        "Andras Kovi",
        "Angel Leon",
        "Ashish Upadhyay",
        "Astreptocoque",
        "Bogdan",
        "Brendo Paulino dos Santos",
        "Bruno Tiago",
        "Cas Sprenger",
        "conql",
        "Damian",
        "Danylo",
        "David",
        "Edi",
        "Erik",
        "Francesco",
        "Geroncity2201",
        "gidano",
        "Grega",
        "João Tavares",
        "kant",
        "Kenneth",
        "lzlesak",
        "Marcello",
        "Marcelo Caetano Mello",
        "Martinet101",
        "Michał 'kwiateusz' Kwiatek",
        "Mr. BURR!",
        "nvi9",
        "Peter Veres",
        "Platon Ostanin",
        "RaFaeLL0",
        "Rafiuddin",
        "Thomas",
        "Tiberiu",
        "Tomas Øvrebust",
        "wrongway",
        "x10102",
        "xvzhenduo",
        "Zaptyp (Patryk)",
        "Андрей",
        "י. פל.",
        "赵大官人",
    ];

    public IReadOnlyList<string> Contributors => contributorNames;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public SettingsViewTranslations()
    {
        this.InitializeComponent();
        Ioc.Default.GetService<ILogService>()?.Log.Information("View loaded");
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        ViewModel = e.Parameter as SettingsViewModel;
    }

    private async void SettingsCardHelpTranslate_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(AppConfig.HelpTranslateUri);
    }
}
