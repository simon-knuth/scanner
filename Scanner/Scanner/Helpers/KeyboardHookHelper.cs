using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace Scanner.Helpers;

public static class KeyboardHookHelper
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DECLARATIONS /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    #region Events
    public static event EventHandler<Windows.System.VirtualKey> KeyPressed;
    #endregion

    #region Constants
    private const int KF_UP = 0x8000;
    #endregion

    private static HHOOK hookHandle;
    private static WNDPROC? oldWndProc;
    private static Window? window;
    private static DispatcherQueue? dispatcherQueue;

    private static WndProcDelegate? wndProcDelegate;
    private static HOOKPROC? hookProcDelegate;


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // METHODS //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static void Initialize(Window window)
    {
        if (hookHandle != 0)
            throw new InvalidOperationException("Hook is already installed. Call Unhook() first.");

        KeyboardHookHelper.window = window ?? throw new ArgumentNullException(nameof(window));
        dispatcherQueue = window.DispatcherQueue;

        hookProcDelegate = HookProcedure;
        hookHandle = PInvoke.SetWindowsHookEx(
            WINDOWS_HOOK_ID.WH_KEYBOARD,
            hookProcDelegate,
            new HINSTANCE(0),
            PInvoke.GetCurrentThreadId());

        if (hookHandle == 0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx failed.");

        wndProcDelegate = WndProc;
        oldWndProc = SetWndProc(window, new IntPtr(Marshal.GetFunctionPointerForDelegate(wndProcDelegate)));
    }

    public static void Unhook()
    {
        if (hookHandle != 0)
        {
            PInvoke.UnhookWindowsHookEx(hookHandle);
            hookHandle = new();
        }

        if (window != null && oldWndProc != null)
        {
            RestoreWndProc(window, Marshal.GetFunctionPointerForDelegate(oldWndProc));
            oldWndProc = new((hWnd, message, wParam, lParam) => new());
        }

        hookProcDelegate = null;
        wndProcDelegate = null;
        window = null;
        dispatcherQueue = null;
    }

    private static WNDPROC SetWndProc(Window window, IntPtr newProc)
    {
        HWND hWnd = new(WindowNative.GetWindowHandle(window));

#if WIN64
        nint oldProc = PInvoke.SetWindowLongPtr(hWnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, newProc);
#else
        nint oldProc = PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, (int)newProc);
#endif

        return Marshal.GetDelegateForFunctionPointer<WNDPROC>(oldProc);
    }

    private static void RestoreWndProc(Window window, nint previousProc)
    {
        HWND hWnd = new(WindowNative.GetWindowHandle(window));

#if WIN64
        PInvoke.SetWindowLongPtr(hWnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, previousProc);
#else
        PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, (int)previousProc);
#endif
    }

    private static LRESULT WndProc(HWND hWnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        return PInvoke.CallWindowProc(oldWndProc, hWnd, message, wParam, lParam);
    }

    private static LRESULT HookProcedure(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0)
        {
            bool isKeyUp = (HIWORD(lParam) & KF_UP) != 0;

            if (!isKeyUp)
            {
                var key = (Windows.System.VirtualKey)(int)wParam.Value;

                dispatcherQueue?.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                {
                    KeyPressed?.Invoke(null, key);
                });
            }
        }

        return PInvoke.CallNextHookEx(hookHandle, code, wParam, lParam);
    }

    private static int HIWORD(IntPtr value) => (int)((value.ToInt64() >> 16) & 0xFFFF);


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MISCELLANEOUS ////////////////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private delegate LRESULT WndProcDelegate(HWND hWnd, uint message, WPARAM wParam, LPARAM lParam);

    private delegate int HookProc(int nCode, WPARAM wParam, nint lParam);
}