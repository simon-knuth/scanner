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
        public readonly IScanService ScanService = Ioc.Default.GetRequiredService<IScanService>();
        public readonly IScanResultService ScanResultService = Ioc.Default.GetRequiredService<IScanResultService>();

        public AsyncRelayCommand<string> RotatePageCommand;
        public AsyncRelayCommand<string> RenameCommand;
        public AsyncRelayCommand CopyCommand;
        public RelayCommand EnterCropModeCommand;
        public RelayCommand LeaveCropModeCommand;
        public RelayCommand EnterDrawModeCommand;
        public RelayCommand LeaveDrawModeCommand;

        public event EventHandler CropSuccessful;
        public event EventHandler RotateSuccessful;
        public event EventHandler DrawSuccessful;
        public event EventHandler RenameSuccessful;
        public event EventHandler DeleteSuccessful;
        public event EventHandler CopySuccessful;

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

        private EditorMode _EditorMode = EditorMode.Initial;
        public EditorMode EditorMode
        {
            get => _EditorMode;
            set
            {
                SetProperty(ref _EditorMode, value);
                if (value == EditorMode.Initial) IsEditing = false;
                else IsEditing = true;
            }
        }

        private bool _IsScanResultChanging;
        public bool IsScanResultChanging
        {
            get => _IsScanResultChanging;
            set => SetProperty(ref _IsScanResultChanging, value);
        }

        private bool _IsScanning;
        public bool IsScanning
        {
            get => _IsScanning;
            set => SetProperty(ref _IsScanning, value);
        }

        private bool _IsEditing;
        public bool IsEditing
        {
            get => _IsEditing;
            set
            {
                bool old = _IsEditing;

                if (old != value)
                {
                    SetProperty(ref _IsEditing, value);
                    Messenger.Send(new EditorIsEditingChangedMessage(value));
                }
            }
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
            IsScanResultChanging = ScanResultService.IsScanResultChanging;
            ScanResultService.ScanResultChanging += (x, y) => IsScanResultChanging = true;
            ScanResultService.ScanResultChanged += (x, y) => IsScanResultChanging = false;

            IsScanning = ScanService.IsScanInProgress;
            ScanService.ScanStarted += (x, y) => IsScanning = true;
            ScanService.ScanEnded += (x, y) => IsScanning = false;

            Messenger.Register<EditorCurrentIndexRequestMessage>(this, (r, m) => m.Reply(SelectedPageIndex));
            Messenger.Register<PageListCurrentIndexChangedMessage>(this, (r, m) => SelectedPageIndex = m.Value);
            Messenger.Register<EditorIsEditingRequestMessage>(this, (r, m) => m.Reply(IsEditing));

            RotatePageCommand = new AsyncRelayCommand<string>((x) => RotatePageAsync((BitmapRotation)int.Parse(x)));
            RenameCommand = new AsyncRelayCommand<string>((x) => RenameAsync(x));
            CopyCommand = new AsyncRelayCommand(CopyAsync);
            EnterCropModeCommand = new RelayCommand(EnterCropMode);
            LeaveCropModeCommand = new RelayCommand(LeaveCropMode);
            EnterDrawModeCommand = new RelayCommand(EnterDrawMode);
            LeaveDrawModeCommand = new RelayCommand(LeaveDrawMode);
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

            bool success = await ScanResultService.RotatePagesAsync(instructions);

            if (!success) return;

            RotateSuccessful?.Invoke(this, EventArgs.Empty);
        }

        private async Task RenameAsync(string newName)
        {
            bool success;
            
            if (ScanResultService.Result.IsImage())
            {
                // rename single image file
                success = await ScanResultService.RenameAsync(SelectedPageIndex, newName);
            }
            else
            {
                // rename PDF document
                success = await ScanResultService.RenameAsync(newName);
            }

            if (!success) return;

            // broadcast new name
            BroadcastSelectedPageTitle();

            RenameSuccessful?.Invoke(this, EventArgs.Empty);
        }

        private void EnterCropMode()
        {
            EditorMode = EditorMode.Crop;
        }

        private void LeaveCropMode()
        {
            EditorMode = EditorMode.Initial;
        }

        private void EnterDrawMode()
        {
            EditorMode = EditorMode.Draw;
        }

        private void LeaveDrawMode()
        {
            EditorMode = EditorMode.Initial;
        }

        private async Task CopyAsync()
        {
            bool success;

            if (ScanResultService.Result.IsImage())
            {
                // copy single image file
                success = await ScanResultService.CopyImageAsync(SelectedPageIndex);
            }
            else
            {
                // rename PDF document
                success = await ScanResultService.CopyAsync();
            }

            if (!success) return;

            CopySuccessful?.Invoke(this, EventArgs.Empty);
        }
    }

    public enum EditorMode
    {
        Initial = 0,
        Crop = 1,
        Draw = 2,
    }
}
