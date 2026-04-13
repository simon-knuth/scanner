using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Win32.SafeHandles;
using Microsoft.Windows.AppLifecycle;
using Scanner;
using SQLitePCL;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Win32;
using Windows.Win32.Foundation;

public class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        bool isRedirect = DecideRedirection();

        if (!isRedirect)
        {
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }

        return 0;
    }

    private static bool DecideRedirection()
    {
        bool isRedirect = false;
        AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
        ExtendedActivationKind kind = args.Kind;
        AppInstance keyInstance = AppInstance.FindOrRegisterForKey("MySingleInstanceApp");

        if (keyInstance.IsCurrent)
        {
            keyInstance.Activated += OnActivated;
        }
        else
        {
            isRedirect = true;
            RedirectActivationTo(args, keyInstance);
        }

        return isRedirect;
    }

    private static SafeFileHandle redirectEventHandle;

    // Do the redirection on another thread, and use a non-blocking
    // wait method to wait for the redirection to complete.
    public static void RedirectActivationTo(AppActivationArguments args,
                                            AppInstance keyInstance)
    {
        redirectEventHandle = PInvoke.CreateEvent(null, true, false, null);
        Task.Run(() =>
        {
            keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
            PInvoke.SetEvent(redirectEventHandle);
        });

        uint CWMO_DEFAULT = 0;
        uint INFINITE = 0xFFFFFFFF;
        _ = PInvoke.CoWaitForMultipleObjects(
           CWMO_DEFAULT, INFINITE, [(HANDLE)redirectEventHandle.DangerousGetHandle()], out uint handleIndex);
        GC.KeepAlive(redirectEventHandle);

        // Bring the window to the foreground
        Process process = Process.GetProcessById((int)keyInstance.ProcessId);
        PInvoke.SetForegroundWindow(new(process.MainWindowHandle));
    }

    private static void OnActivated(object sender, AppActivationArguments args)
    {
        ExtendedActivationKind kind = args.Kind;
    }
}