using CommunityToolkit.Mvvm.ComponentModel;
using MuseWiser.Services.Audio;

namespace MuseWiser.ViewModels;

/// <summary>
/// 既定の再生デバイスの名前・サンプルレート・チャンネル数を表示するための ViewModel。
/// <see cref="IAudioCaptureService.DeviceChanged"/> を購読し、既定デバイスの切り替えに追従する。
/// </summary>
public sealed partial class AudioDeviceInfoViewModel : ViewModelBase, IDisposable
{
    private readonly IAudioCaptureService _captureService;

    [ObservableProperty]
    private bool _hasDevice;

    [ObservableProperty]
    private string _deviceName = string.Empty;

    [ObservableProperty]
    private string _displayText = "既定の再生デバイスが見つかりません";

    public AudioDeviceInfoViewModel(IAudioCaptureService captureService)
    {
        _captureService = captureService;
        _captureService.DeviceChanged += OnDeviceChanged;
    }

    private void OnDeviceChanged(object? sender, AudioCaptureDeviceInfo info)
    {
        if (string.IsNullOrEmpty(info.DeviceName))
        {
            HasDevice = false;
            DeviceName = string.Empty;
            DisplayText = "既定の再生デバイスが見つかりません";
            return;
        }

        HasDevice = true;
        DeviceName = info.DeviceName;
        DisplayText = $"{info.DeviceName}（{FormatSampleRate(info.SampleRate)} {FormatChannels(info.Channels)}）";
    }

    private static string FormatSampleRate(int sampleRate)
    {
        var kiloHertz = sampleRate / 1000.0;
        return $"{kiloHertz:0.0}KHz";
    }

    private static string FormatChannels(int channels) => channels switch
    {
        1 => "Mono",
        2 => "Stereo",
        _ => "Multi",
    };

    public void Dispose()
    {
        _captureService.DeviceChanged -= OnDeviceChanged;
    }
}
