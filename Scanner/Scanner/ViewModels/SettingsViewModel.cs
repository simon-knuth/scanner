using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Scanner.Models;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.ViewModels
{
    public partial class SettingsViewModel : ObservableRecipient, IDisposable
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
        public readonly ISettingsService SettingsService = Ioc.Default.GetService<ISettingsService>();
        #endregion

        #region Commands
        public RelayCommand DisposeCommand => new RelayCommand(Dispose);
        #endregion

        public SettingsPage[] HeaderSettingsPages =
        [
            new SettingsPage(SettingsPageType.General, "\uE713", "General"),
            new SettingsPage(SettingsPageType.Personalization, "\uE771", "Personalization"),
            new SettingsPage(SettingsPageType.Privacy, "\uEA18", "Privacy"),
        ];

        public SettingsPage[] FooterSettingsPages =
        [
            new SettingsPage(SettingsPageType.Feedback, "\uED15", "Feedback"),
            new SettingsPage(SettingsPageType.About, "\uE946", "About"),
        ];

        [ObservableProperty]
        private SettingsPage selectedPage;

        public int SettingScanAction
        {
            get => (int)SettingsService.SettingScanAction;
            set => SettingsService.SettingScanAction = (SettingScanAction)value;
        }

        public int SettingAppTheme
        {
            get => (int)SettingsService.SettingAppTheme;
            set => SettingsService.SettingAppTheme = (SettingAppTheme)value;
        }

        public int SettingMeasurementUnits
        {
            get => (int)SettingsService.SettingMeasurementUnits;
            set => SettingsService.SettingMeasurementUnits = (SettingMeasurementUnits)value;
        }

        public int SettingEditorOrientation
        {
            get => (int)SettingsService.SettingEditorOrientation;
            set => SettingsService.SettingEditorOrientation = (SettingEditorOrientation)value;
        }

        public string CurrentVersion => Helpers.Helpers.GetCurrentVersion();


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public SettingsViewModel()
        {
            SelectedPage = HeaderSettingsPages[0];
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public void Dispose()
        {
            Messenger.UnregisterAll(this);
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public record SettingsPage(SettingsPageType PageType, string Glyph, string FriendlyName);

    public enum SettingsPageType
    {
        General,
        Personalization,
        Privacy,
        Feedback,
        About
    }
}
