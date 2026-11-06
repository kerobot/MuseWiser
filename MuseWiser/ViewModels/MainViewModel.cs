namespace MuseWiser.ViewModels;

/// <summary>
/// メインウィンドウ全体の ViewModel。各機能の ViewModel を集約する。
/// </summary>
public sealed class MainViewModel : ViewModelBase, IAsyncDisposable
{
    public ClockViewModel Clock { get; }

    public WeatherViewModel Weather { get; }

    public VolumeMeterViewModel VolumeMeter { get; }

    public AudioDeviceInfoViewModel AudioDevice { get; }

    public MainViewModel(ClockViewModel clock, WeatherViewModel weather, VolumeMeterViewModel volumeMeter, AudioDeviceInfoViewModel audioDevice)
    {
        Clock = clock;
        Weather = weather;
        VolumeMeter = volumeMeter;
        AudioDevice = audioDevice;
        Weather.Start();
        VolumeMeter.Start();
    }

    public async ValueTask DisposeAsync()
    {
        AudioDevice.Dispose();
        VolumeMeter.Dispose();
        Clock.Dispose();
        await Weather.DisposeAsync();
    }
}
