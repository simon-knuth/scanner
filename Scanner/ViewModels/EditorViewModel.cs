using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Controls;
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

        public AsyncRelayCommand<string> RotatePageCommand;

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
            set
            {
                SetProperty(ref _SelectedPage, value);
                BroadcastSelectedPageTitle();
            }
        }

        private int _SelectedPageIndex;
        public int SelectedPageIndex
        {
            get => _SelectedPageIndex;
            set
            {
                int old = _SelectedPageIndex;
                
                if (old != value)
                {
                    SetProperty(ref _SelectedPageIndex, value);
                    if (value != -1) Messenger.Send(new EditorCurrentIndexChangedMessage(value));
                }

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

            Messenger.Register<EditorCurrentIndexRequestMessage>(this, (r, m) => m.Reply(SelectedPageIndex));
            Messenger.Register<PageListCurrentIndexChangedMessage>(this, (r, m) => SelectedPageIndex = m.Value);

            RotatePageCommand = new AsyncRelayCommand<string>((x) => RotatePageAsync((BitmapRotation)int.Parse(x)));
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void RefreshOrientationSetting()
        {
            int orientationValue = (int)SettingsService.GetSetting(AppSetting.SettingEditorOrientation);

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

        private void BroadcastSelectedPageTitle()
        {
            if (SelectedPage == null || SelectedPage.ScanFile == null) return;

            Messenger.Send(new EditorSelectionTitleChangedMessage
            {
                Title = SelectedPage?.ScanFile?.Name
            });
        }

        private async Task RotatePageAsync(BitmapRotation rotation)
        {
            List<Tuple<int, BitmapRotation>> instructions = new List<Tuple<int, BitmapRotation>>
            {
                new Tuple<int, BitmapRotation>(SelectedPageIndex, rotation)
            };

            await ScanResultService.RotatePagesAsync(instructions);
        }
    }
}
