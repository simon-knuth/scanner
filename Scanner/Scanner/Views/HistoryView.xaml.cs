using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Scanner.Models;
using Scanner.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
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

    #region Fields
    private static readonly SemaphoreSlim thumbnailGenerationLock = new(1, 1);
    #endregion


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public HistoryView()
    {
        this.InitializeComponent();
        GroupedEntriesViewSource.Source = ViewModel.GroupedEntries;
        Ioc.Default.GetService<ILogService>()?.Log.Information("View loaded");
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        await ViewModel.OpenEntryAsync((ProjectHistoryEntry)e.ClickedItem);
    }

    private async void MenuFlyoutItemOpen_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not ProjectHistoryEntry entry)
            return;

        CloseRequested?.Invoke(this, EventArgs.Empty);
        await ViewModel.OpenEntryAsync(entry);
    }

    private async void ImageHistoryEntry_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is not ProjectHistoryEntry historyEntry)
            return;

        if (historyEntry.AreFilesMissing || historyEntry.Files is not { Count: > 0 })
            return;

        Image image = (Image)sender;

        try
        {
            // get first file's thumbnail
            BitmapImage thumbnail;
            if (historyEntry.Format is TargetFormat.PDF)
            {
                await thumbnailGenerationLock.WaitAsync();
                try
                {
                    // check container wasn't recycled while waiting
                    if (sender.DataContext != historyEntry)
                        return;

                    thumbnail = new BitmapImage();
                    StorageFile file = await StorageFile.GetFileFromPathAsync(historyEntry.Files[0].FilePath);
                    using IRandomAccessStream pdfStream = await file.OpenReadAsync();
                    PdfDocument document = await PdfDocument.LoadFromStreamAsync(pdfStream);
                    using InMemoryRandomAccessStream bitmapStream = new();
                    PdfPageRenderOptions renderOptions = new()
                    {
                        DestinationHeight = 72
                    };
                    using (Windows.Data.Pdf.PdfPage page = document.GetPage(0))
                    {
                        await page.RenderToStreamAsync(bitmapStream, renderOptions);
                    }
                    await thumbnail.SetSourceAsync(bitmapStream);
                }
                finally
                {
                    thumbnailGenerationLock.Release();
                }
            }
            else
            {
                thumbnail = new BitmapImage(new Uri("file:///" + historyEntry.Files[0].FilePath.Replace('\\', '/')));
                thumbnail.DecodePixelType = DecodePixelType.Logical;
                thumbnail.DecodePixelHeight = 72;
            }

            // only apply if container wasn't recycled up until now
            if (sender.DataContext == historyEntry)
                image.Source = thumbnail;
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
