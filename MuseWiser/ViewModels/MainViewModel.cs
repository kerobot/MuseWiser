namespace MuseWiser.ViewModels;

/// <summary>
/// メインウィンドウ全体の ViewModel。各機能の ViewModel を集約する。
/// </summary>
public sealed class MainViewModel : ViewModelBase, IAsyncDisposable
{
    public ClockViewModel Clock { get; }

    public WeatherViewModel Weather { get; }

    public VolumeMeterViewModel VolumeMeter { get; }

    public MainViewModel(ClockViewModel clock, WeatherViewModel weather, VolumeMeterViewModel volumeMeter)
    {
        Clock = clock;
        Weather = weather;
        VolumeMeter = volumeMeter;
        Weather.Start();
        VolumeMeter.Start();
    }

    public async ValueTask DisposeAsync()
    {
        VolumeMeter.Dispose();
        Clock.Dispose();
        await Weather.DisposeAsync();
    }
}
