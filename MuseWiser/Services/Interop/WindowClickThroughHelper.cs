using System.Runtime.InteropServices;

namespace MuseWiser.Services.Interop;

/// <summary>
/// ウィンドウをクリックスルー（他ウィンドウの操作を妨げない）にするための Win32 相互運用ヘルパー。
/// </summary>
public static class WindowClickThroughHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static long GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return Environment.Is64BitProcess
            ? GetWindowLongPtr64(hWnd, nIndex).ToInt64()
            : GetWindowLong32(hWnd, nIndex);
    }

    private static void SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (Environment.Is64BitProcess)
        {
            SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        }
        else
        {
            SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32());
        }
    }

    /// <summary>
    /// 指定したウィンドウハンドルに対し、クリック・マウス操作を透過させる拡張スタイルを設定する。
    /// 設定後、このウィンドウはマウス操作を受け付けず、背面のウィンドウを直接操作できるようになる。
    /// </summary>
    public static void MakeClickThrough(IntPtr windowHandle)
    {
        var extendedStyle = GetWindowLongPtr(windowHandle, GWL_EXSTYLE);
        var newStyle = extendedStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT;
        SetWindowLongPtr(windowHandle, GWL_EXSTYLE, new IntPtr(newStyle));
    }

    /// <summary>
    /// 指定したウィンドウハンドルを Alt + Tab の切り替え候補から除外する拡張スタイルを設定する。
    /// </summary>
    public static void ExcludeFromAltTab(IntPtr windowHandle)
    {
        var extendedStyle = GetWindowLongPtr(windowHandle, GWL_EXSTYLE);
        var newStyle = extendedStyle | WS_EX_TOOLWINDOW;
        SetWindowLongPtr(windowHandle, GWL_EXSTYLE, new IntPtr(newStyle));
    }
}
