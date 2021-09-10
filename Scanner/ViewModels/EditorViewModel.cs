using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using WinUI = Microsoft.UI.Xaml.Controls;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using static Scanner.Services.Messenger.MessengerEnums;
using Windows.UI.Xaml.Controls;
using static HelpViewEnums;
using Microsoft.Toolkit.Mvvm.Input;
using Scanner.Services;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using static Scanner.Services.SettingsEnums;
using static Utilities;

namespace Scanner.ViewModels
{
    public class EditorViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private readonly ISettingsService SettingsService = Ioc.Default.GetRequiredService<ISettingsService>();
        public readonly IScanResultService ScanResultService = Ioc.Default.GetRequiredService<IScanResultService>();

        private Orientation _Orientation;
        public Orientation Orientation
        {
            get => _Orientation;
            set => SetProperty(ref _Orientation, value);
        }

        private ScanResult _ScanResult;
        public ScanResult ScanResult
        {
            get => _ScanResult;
            set => SetProperty(ref _ScanResult, value);
        }

        private ScanResultElement _SelectedPage;
        public ScanResultElement SelectedPage
        {
            get => _SelectedPage;
            set => SetProperty(ref _SelectedPage, value);
        }

        private int _SelectedPageIndex;
        public int SelectedPageIndex
        {
            get => _SelectedPageIndex;
            set
            {
                SetProperty(ref _SelectedPageIndex, value);
                RefreshSelectedPageText();
            }
        }

        private string _SelectedPageText;
        public string SelectedPageText
        {
            get => _SelectedPageText;
            set => SetProperty(ref _SelectedPageText, value);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public EditorViewModel()
        {
            RefreshOrientationSetting();
            SettingsService.SettingChanged += SettingsService_SettingChanged;
            ScanResultService.ScanResultCreated += ScanResultService_ScanResultCreated;
            ScanResultService.ScanResultDismissed += ScanResultService_ScanResultDismissed;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void RefreshOrientationSetting()
        {
            int orientationValue = (int) SettingsService.GetSetting(AppSetting.SettingEditorOrientation);

            switch (orientationValue)
            {
                case 0:
                    Orientation = Orientation.Horizontal;
                    break;
                case 1:
                    Orientation = Orientation.Vertical;
                    break;
                default:
                    break;
            }
        }
        
        private void SettingsService_SettingChanged(object sender, AppSetting e)
        {
            if (e == AppSetting.SettingEditorOrientation)
            {
                RefreshOrientationSetting();
            }
        }

        private void ScanResultService_ScanResultDismissed(object sender, EventArgs e)
        {
            ScanResult = null;
        }

        private void ScanResultService_ScanResultCreated(object sender, ScanResult scanResult)
        {
            ScanResult = scanResult;
        }

        private void RefreshSelectedPageText()
        {
            if (SelectedPageIndex >= 0 && ScanResult != null)
            {
                SelectedPageText = String.Format(LocalizedString("TextPageIndicator"),
                    SelectedPageIndex + 1, ScanResult.NumberOfPages);
            }
            else
            {
                SelectedPageText = "";
            }
        }
    }
}
