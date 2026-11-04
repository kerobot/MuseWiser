using System.Windows;
using MuseWiser.Models;
using MuseWiser.Models.Enums;

namespace MuseWiser.Services.Interop;

/// <summary>
/// <see cref="DisplaySettings"/> からウィンドウの配置位置・サイズを計算するヘルパー。
/// </summary>
public static class WindowPositionHelper
{
    /// <summary>
    /// ウィンドウに設定可能な幅・高さの最小値（ピクセル）。
    /// 設定ファイル破損等で 0 以下の値が渡された場合の防御に使用する。
    /// </summary>
    private const double MinWindowSize = 100;

    /// <summary>
    /// 指定した設定をウィンドウへ適用する（Left/Top/Width/Height を設定）。
    /// </summary>
    public static void Apply(Window window, DisplaySettings settings)
    {
        var monitor = MonitorHelper.ResolveMonitor(settings.MonitorDeviceName);
        var scale = GetDpiScale(window);

        // System.Drawing の座標はデバイスピクセルのため、WPF の論理ピクセルへ変換する。
        var workingArea = new Rect(
            monitor.WorkingArea.X / scale.X,
            monitor.WorkingArea.Y / scale.Y,
            monitor.WorkingArea.Width / scale.X,
            monitor.WorkingArea.Height / scale.Y);

        var width = Math.Max(MinWindowSize, settings.Width);
        var height = Math.Max(MinWindowSize, settings.Height);

        var (anchorX, anchorY) = GetAnchorOrigin(settings.Anchor, workingArea, width, height);

        window.Width = width;
        window.Height = height;
        window.Left = anchorX + settings.OffsetX;
        window.Top = anchorY + settings.OffsetY;
    }

    private static (double X, double Y) GetAnchorOrigin(ScreenAnchor anchor, Rect workingArea, double width, double height)
    {
        double x = anchor switch
        {
            ScreenAnchor.TopLeft or ScreenAnchor.MiddleLeft or ScreenAnchor.BottomLeft => workingArea.Left,
            ScreenAnchor.TopCenter or ScreenAnchor.MiddleCenter or ScreenAnchor.BottomCenter => workingArea.Left + ((workingArea.Width - width) / 2),
            ScreenAnchor.TopRight or ScreenAnchor.MiddleRight or ScreenAnchor.BottomRight => workingArea.Right - width,
            _ => workingArea.Left,
        };

        double y = anchor switch
        {
            ScreenAnchor.TopLeft or ScreenAnchor.TopCenter or ScreenAnchor.TopRight => workingArea.Top,
            ScreenAnchor.MiddleLeft or ScreenAnchor.MiddleCenter or ScreenAnchor.MiddleRight => workingArea.Top + ((workingArea.Height - height) / 2),
            ScreenAnchor.BottomLeft or ScreenAnchor.BottomCenter or ScreenAnchor.BottomRight => workingArea.Bottom - height,
            _ => workingArea.Top,
        };

        return (x, y);
    }

    private static System.Windows.Point GetDpiScale(Window window)
    {
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget is not null)
        {
            var matrix = source.CompositionTarget.TransformToDevice;
            return new System.Windows.Point(matrix.M11, matrix.M22);
        }

        return new System.Windows.Point(1.0, 1.0);
    }
}
