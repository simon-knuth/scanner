using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace Scanner
{
    class FlipViewScanItemData
    {
        public BitmapImage image { get; set; }


        public FlipViewScanItemData(BitmapImage image)
        {
            this.image = image;
        }
    }
}
