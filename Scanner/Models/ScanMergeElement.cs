using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Scanner.Services;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using static Utilities;


namespace Scanner
{
    public class ScanMergeElement : ObservableObject
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private BitmapImage _Thumbnail;
        public BitmapImage Thumbnail
        {
            get => _Thumbnail;
            set => SetProperty(ref _Thumbnail, value);
        }

        private string _ItemDescriptor;
        public string ItemDescriptor
        {
            get => _ItemDescriptor;
            set => SetProperty(ref _ItemDescriptor, value);
        }

        private bool _IsPotentialPage;
        public bool IsPotentialPage
        {
            get => _IsPotentialPage;
            set => SetProperty(ref _IsPotentialPage, value);
        }

        private bool _IsStartPage;
        public bool IsStartPage
        {
            get => _IsStartPage;
            set => SetProperty(ref _IsStartPage, value);
        }

        private bool _IsOrderReversed;
        public bool IsOrderReversed
        {
            get => _IsOrderReversed;
            set => SetProperty(ref _IsOrderReversed, value);
        }

        private bool _IsPlaceholderForMultiplePages;
        public bool IsPlaceholderForMultiplePages
        {
            get => _IsPlaceholderForMultiplePages;
            set => SetProperty(ref _IsPlaceholderForMultiplePages, value);
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public ScanMergeElement()
        {
            
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


    }
}
