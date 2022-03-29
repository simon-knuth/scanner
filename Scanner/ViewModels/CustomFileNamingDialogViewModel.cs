using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.Helpers;
using Scanner.Models.FileNaming;
using Scanner.Services;
using Scanner.Services.Messenger;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using static Utilities;

namespace Scanner.ViewModels
{
    public class CustomFileNamingDialogViewModel : ObservableRecipient
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        #region Services
        public readonly IAccessibilityService AccessibilityService = Ioc.Default.GetService<IAccessibilityService>();
        public readonly IAppCenterService AppCenterService = Ioc.Default.GetService<IAppCenterService>();
        public readonly ILogService LogService = Ioc.Default.GetService<ILogService>();
        #endregion

        #region Commands
        public RelayCommand AcceptCommand => new RelayCommand(AcceptPattern);
        public RelayCommand CancelCommand => new RelayCommand(Cancel);
        #endregion

        #region Events
        public event EventHandler CloseRequested;
        #endregion

        private List<string> _AvailableBlocks;
        public List<string> AvailableBlocks
        {
            get => _AvailableBlocks;
            set => SetProperty(ref _AvailableBlocks, value);
        }

        private ObservableCollection<IFileNamingBlock> _SelectedBlocks = new ObservableCollection<IFileNamingBlock>();
        public ObservableCollection<IFileNamingBlock> SelectedBlocks
        {
            get => _SelectedBlocks;
            set => SetProperty(ref _SelectedBlocks, value);
        }

        private FileNamingPattern _Pattern;


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public CustomFileNamingDialogViewModel()
        {
            AvailableBlocks = FileNamingStatics.FileNamingBlocksDictionary.Keys.ToList();
            SelectedBlocks.Add(new ResolutionFileNamingBlock());
            SelectedBlocks.Add(new FileTypeFileNamingBlock());
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void AcceptPattern()
        {

        }

        private void Cancel()
        {

        }
    }
}
