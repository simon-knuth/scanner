using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;


namespace Scanner.Views;

public sealed partial class HistoryView : Page
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Events
    public event EventHandler CloseRequested;
    #endregion


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public HistoryView()
    {
        this.InitializeComponent();
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        await ViewModel.OpenEntryAsync((ProjectHistoryEntry)e.ClickedItem);
    }

    private async void ImageHistoryEntry_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is not ProjectHistoryEntry historyEntry)
            return;

        if (historyEntry.AreFilesMissing)
            return;

        try
        {
            // get first file's thumbnail
            if (historyEntry.Format is TargetFormat.PDF)
            {
                BitmapImage image = new BitmapImage();
                StorageFile file = await StorageFile.GetFileFromPathAsync(historyEntry.Files[0].FilePath);
                using IRandomAccessStream pdfStream = await file.OpenReadAsync();
                PdfDocument document = await PdfDocument.LoadFromStreamAsync(pdfStream);
                using (InMemoryRandomAccessStream bitmapStream = new())
                {
                    PdfPageRenderOptions renderOptions = new()
                    {
                        DestinationHeight = 72
                    };
                    await document.GetPage(0).RenderToStreamAsync(bitmapStream, renderOptions);
                    await image.SetSourceAsync(bitmapStream);
                }

                ((Image)sender).Source = image;
            }
            else
            {
                BitmapImage image = new BitmapImage(new Uri("file:///" + historyEntry.Files[0].FilePath.Replace('\\', '/')));
                image.DecodePixelType = DecodePixelType.Logical;
                image.DecodePixelHeight = 72;
                ((Image)sender).Source = image;
            }
        }
        catch (Exception)
        {

        }
    }

    private void Page_Loading(FrameworkElement sender, object args)
    {
        ViewModel.ViewLoaded(DispatcherQueue);
    }
}
