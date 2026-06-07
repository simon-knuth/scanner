using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using Scanner.Services.Interfaces;
using Scanner.Models.Interfaces;
using System.Threading;
using Windows.Devices.Enumeration;
using CommunityToolkit.Mvvm.DependencyInjection;
using Windows.Devices.Scanners;
using Scanner.Models;
using Scanner.Models.ScanningDevices;

namespace Scanner.Services;

internal partial class ScannerDiscoveryService : IScannerDiscoveryService
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Services
    private readonly ILogService? LogService = Ioc.Default.GetService<ILogService>();
    private readonly ISentryService? SentryService = Ioc.Default.GetService<ISentryService>();
    #endregion

    #region Events
    public event EventHandler InitialCrawlCompleted;
    public event EventHandler<IScanningDevice> ScanningDeviceFound;
    public event EventHandler<IScanningDevice> ScanningDeviceLost;
    #endregion

    public TaskCompletionSource InitialCrawlCompletion
    {
        get;
    } = new();

    private readonly List<IScanningDevice> Devices = new();
    private readonly SemaphoreSlim semaphoreDevices = new SemaphoreSlim(1, 1);

    private DeviceWatcher watcher;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // CONSTRUCTORS / FACTORIES /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ScannerDiscoveryService()
    {
        
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public async Task<List<IScanningDevice>> GetScanningDevicesAsync()
    {
        await semaphoreDevices.WaitAsync();

        List<IScanningDevice> result = Devices.ToList();

        semaphoreDevices.Release();

        return result;
    }

    public async Task InitializeSearchAsync()
    {
        watcher?.Stop();

        await semaphoreDevices.WaitAsync();

        Devices.Clear();

        semaphoreDevices.Release();

        // trigger search (effectiveness unclear)
        await DeviceInformation.FindAllAsync(DeviceClass.ImageScanner);

        // set up device watcher
        watcher = DeviceInformation.CreateWatcher(DeviceClass.ImageScanner);

        watcher.Added += Watcher_ScannerFound;
        watcher.Removed += Watcher_ScannerLost;
        watcher.EnumerationCompleted += Watcher_EnumerationCompleted;

        watcher.Start();
        LogService?.Log.Information("Initialized");
    }

    private void Watcher_EnumerationCompleted(DeviceWatcher sender, object args)
    {
        InitialCrawlCompleted?.Invoke(this, EventArgs.Empty);
        InitialCrawlCompletion.TrySetResult();
    }

    private async void Watcher_ScannerFound(DeviceWatcher sender, DeviceInformation args)
    {
        int attempt = 1;
        while (attempt <= 2)
        {
            try
            {
                // construct scanner
                ImageScanner imageScanner = await ImageScanner.FromIdAsync(args.Id);
                HardwareScanner scanner = new HardwareScanner(imageScanner, args.Name);

                await TryAddScannerAsync(scanner);
                LogService?.Log.Information("Found and added {@Scanner}", scanner);

                // analytics
                if (scanner != null) SendScannerAnalytics(scanner);

                return;
            }
            catch (Exception exc)
            {
                LogService?.Log.Warning(exc, "Failed to add scanner ({Attempt})", attempt);
                if (attempt < 2)
                {
                    // scanner may just be blocked by another app, try again
                    await Task.Delay(5000);
                }
                attempt++;
            }
        }
    }

    private async void Watcher_ScannerLost(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        await TryRemoveScannerByIdAsync(args.Id);
    }

    private async Task TryRemoveScannerAsync(IScanningDevice scanner)
    {
        try
        {
            await semaphoreDevices.WaitAsync();
            Devices.Remove(scanner);
            ScanningDeviceLost?.Invoke(this, scanner);
        }
        catch (Exception exc)
        {
            LogService?.Log.Warning(exc, "Failed to remove {@Device}", scanner);
            return;
        }
        finally
        {
            semaphoreDevices.Release();
        }
    }

    private async Task TryRemoveScannerByIdAsync(string scannerId)
    {
        try
        {
            // search for scanner
            await semaphoreDevices.WaitAsync();
            IScanningDevice? device = Devices.FirstOrDefault((x) => x.Id.ToLower() == scannerId.ToLower());

            // remove scanner
            if (device != null)
            {
                Devices.Remove(device);
                ScanningDeviceLost?.Invoke(this, device);
            }
        }
        catch (Exception exc)
        {
            LogService?.Log.Warning(exc, "Failed to remove device by {Id}", scannerId);
            return;
        }
        finally
        {
            semaphoreDevices.Release();
        }
    }

    public async Task AddDebugScannerAsync(DebugScanner scanner)
    {
        await TryAddScannerAsync(scanner);
    }

    public async Task RemoveDebugScannerAsync(DebugScanner scanner)
    {
        await TryRemoveScannerAsync(scanner);
    }

    private async Task TryAddScannerAsync(IScanningDevice scanner)
    {
        try
        {
            // check for duplicate
            await semaphoreDevices.WaitAsync();
            if (Devices.Exists((x) => x.Id.ToLower() == scanner.Id.ToLower()))
            {
                // duplicate detected ~> ignore
                LogService?.Log.Information("Found duplicate {@Scanner}", scanner);
                return;
            }

            // add scanner
            Devices.Add(scanner);
            ScanningDeviceFound?.Invoke(this, scanner);
        }
        catch (Exception exc)
        {
            LogService?.Log.Warning(exc, "Failed to add discovered {@Scanner}", scanner);
            return;
        }
        finally
        {
            semaphoreDevices.Release();
        }
    }

    public void SendScannerAnalytics(HardwareScanner scanner)
    {
        try
        {
            // gather all supported formats across the scanner's source modes
            HashSet<ImageScannerFormat> formats = new();
            if (scanner.AutoFormats != null) formats.UnionWith(scanner.AutoFormats);
            if (scanner.FlatbedFormats != null) formats.UnionWith(scanner.FlatbedFormats);
            if (scanner.FeederFormats != null) formats.UnionWith(scanner.FeederFormats);

            bool jpgSupported = formats.Contains(ImageScannerFormat.Jpeg);
            bool pngSupported = formats.Contains(ImageScannerFormat.Png);
            bool pdfSupported = formats.Contains(ImageScannerFormat.Pdf);
            bool xpsSupported = formats.Contains(ImageScannerFormat.Xps);
            bool oxpsSupported = formats.Contains(ImageScannerFormat.OpenXps);
            bool tifSupported = formats.Contains(ImageScannerFormat.Tiff);
            bool bmpSupported = formats.Contains(ImageScannerFormat.DeviceIndependentBitmap);

            // encode the supported formats as a single combination string
            StringBuilder formatCombination = new();
            if (jpgSupported) formatCombination.Append("|JPG");
            if (pngSupported) formatCombination.Append("|PNG");
            if (pdfSupported) formatCombination.Append("|PDF");
            if (xpsSupported) formatCombination.Append("|XPS");
            if (oxpsSupported) formatCombination.Append("|OXPS");
            if (tifSupported) formatCombination.Append("|TIF");
            if (bmpSupported) formatCombination.Append("|BMP");
            formatCombination.Append("|");

            SentryService?.TrackEvent(AnalyticsEvent.ScannerAdded, new Dictionary<string, string>
            {
                { "format_combination", formatCombination.ToString() },
                { "jpg_supported", jpgSupported.ToString() },
                { "png_supported", pngSupported.ToString() },
                { "pdf_supported", pdfSupported.ToString() },
                { "xps_supported", xpsSupported.ToString() },
                { "oxps_supported", oxpsSupported.ToString() },
                { "tif_supported", tifSupported.ToString() },
                { "bmp_supported", bmpSupported.ToString() },
                { "has_auto", scanner.IsAutoAllowed.ToString() },
                { "has_flatbed", scanner.IsFlatbedAllowed.ToString() },
                { "has_feeder", scanner.IsFeederAllowed.ToString() },
                { "auto_preview_supported", scanner.IsAutoPreviewAllowed.ToString() },
                { "flatbed_preview_supported", scanner.IsFlatbedPreviewAllowed.ToString() },
                { "feeder_preview_supported", scanner.IsFeederPreviewAllowed.ToString() },
                { "feeder_auto_crop_possible", scanner.IsFeederAutoCropAllowed.ToString() },
                { "feeder_auto_crop_single_supported", scanner.IsFeederAutoCropSingleRegionAllowed.ToString() },
                { "feeder_auto_crop_multi_supported", scanner.IsFeederAutoCropMultiRegionAllowed.ToString() },
            });
        }
        catch (Exception exc)
        {
            LogService?.Log.Warning(exc, "Failed to send scanner analytics");
        }
    }
}
