using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MuseWiser.Models;
using MuseWiser.Models.Enums;

namespace MuseWiser.Services.Interop;

/// <summary>
/// <see cref="DisplaySettings"/> の <see cref="WindowTopmostMode"/> に応じて、
/// ウィンドウの Z オーダー（最前面表示/最背面表示）を切り替える Win32 相互運用ヘルパー。
/// WPF の <see cref="Window.Topmost"/> は最前面表示のみをサポートするため、
/// 最背面表示は Win32 の SetWindowPos（HWND_BOTTOM）を使用して実現する。
/// </summary>
public static class WindowZOrderHelper
{
    private static readonly IntPtr HwndBottom = new(1);

    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>
    /// 指定したウィンドウに対し、設定に応じた Z オーダーを適用する。
    /// </summary>
    /// <param name="window">対象ウィンドウ。</param>
    /// <param name="settings">表示設定。</param>
    public static void Apply(Window window, DisplaySettings settings)
    {
        if (settings.WindowTopmostMode == WindowTopmostMode.Topmost)
        {
            window.Topmost = true;
            return;
        }

        window.Topmost = false;

        var windowHandle = new WindowInteropHelper(window).Handle;
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(windowHandle, HwndBottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }
}
