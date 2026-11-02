using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MuseWiser.ViewModels;

/// <summary>
/// 現在日時（時分秒・年月日＋曜日）をリアルタイムに表示するための ViewModel。
/// </summary>
public sealed partial class ClockViewModel : ViewModelBase, IDisposable
{
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private string _timeText = string.Empty;

    [ObservableProperty]
    private string _dateText = string.Empty;

    public ClockViewModel()
    {
        UpdateNow();

        _timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _timer.Tick += (_, _) => UpdateNow();
        _timer.Start();
    }

    private void UpdateNow()
    {
        var now = DateTime.Now;
        TimeText = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        DateText = now.ToString("yyyy-MM-dd(ddd)", CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
