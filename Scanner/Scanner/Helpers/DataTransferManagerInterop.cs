using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.Resources;
using Scanner.Models;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Services.Store;
using Windows.System;
using WinRT.Interop;

namespace Scanner.Helpers
{
    [System.Runtime.InteropServices.ComImport]
    [System.Runtime.InteropServices.Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
    [System.Runtime.InteropServices.InterfaceType(
        System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow([System.Runtime.InteropServices.In] IntPtr appWindow,
            [System.Runtime.InteropServices.In] ref Guid riid);

        void ShowShareUIForWindow(IntPtr appWindow);
    }

    public static class DataTransferManagerInteropGuids
    {
        public static readonly Guid Dtm_iid =
            new Guid(0xa5caee9b, 0x8708, 0x49d1, 0x8d, 0x36, 0x67, 0xd2, 0x5a, 0x8d, 0xa0, 0x0c);
    }
}