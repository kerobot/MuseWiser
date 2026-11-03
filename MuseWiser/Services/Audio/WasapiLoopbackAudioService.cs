using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace MuseWiser.Services.Audio;

/// <summary>
/// 既定の再生デバイス（共有モード）の WASAPI ループバックキャプチャにより、
/// 左右チャンネルの音量レベルを取得するサービス。
/// NAudio 3.x の <see cref="WasapiRecorderBuilder"/> / <see cref="WasapiRecorder"/> を利用する。
/// 既定の再生デバイスが切り替わった場合は、キャプチャ対象を自動的に追従させる。
/// </summary>
public sealed class WasapiLoopbackAudioService : IAudioCaptureService
{
    /// <summary>
    /// FFT サイズの基準値と、その基準となるサンプルレート。
    /// サンプルレートが高いほど同じ時間窓を保つために FFT サイズを大きくし、
    /// 低域の周波数分解能（1 ビンあたりの Hz 幅）を確保する。
    /// </summary>
    private const int BaseFftSize = 2048;

    private const int BaseSampleRate = 44100;

    /// <summary>
    /// FFT サイズの下限・上限（2 のべき乗）。
    /// </summary>
    private const int MinFftLength = 1024;

    private const int MaxFftLength = 16384;

    /// <summary>
    /// 表示するスペクトラムバンド数（PlayMuse 風 16 バンド LED を想定）。
    /// </summary>
    private const int SpectrumBandCount = 16;

    /// <summary>
    /// スペクトラムバンドの境界周波数（Hz）。SpectrumBandCount + 1 個の境界で SpectrumBandCount 個のバンドを定義する。
    /// SPAN 系アナライザーの一般的な目盛りに合わせた固定値。
    /// </summary>
    private static readonly double[] SpectrumBandBoundaries =
    [
        20, 30, 40, 70, 120, 190, 310, 490, 780, 1200, 1900, 3100, 5000, 7900, 12000, 20000, 20000
    ];

    /// <summary>
    /// スペクトラム表示の下限（この dB 以下は無音扱い）。
    /// </summary>
    private const double SpectrumMinDecibels = -70.0;

    private readonly ILogger<WasapiLoopbackAudioService> _logger;
    private readonly object _gate = new();
    private readonly MMDeviceEnumerator _enumerator = new();

    private int _fftSize = BaseFftSize;
    private int _fftPow = (int)Math.Log2(BaseFftSize);
    private float[] _fftCircularBuffer = new float[BaseFftSize];
    private Complex[] _fftWorkBuffer = new Complex[BaseFftSize];
    private int _fftWritePos;
    private int _fftFilledCount;
    private int _fftSamplesSinceLastCompute;
    private int _sampleRate = 44100;
    private float[] _lastSpectrum = new float[SpectrumBandCount];

    private MMDeviceNotificationClient? _notificationClient;
    private WasapiRecorder? _recorder;
    private bool _started;
    private bool _disposed;

    public WasapiLoopbackAudioService(ILogger<WasapiLoopbackAudioService> logger)
    {
        _logger = logger;
    }

    public event EventHandler<AudioCaptureDataEventArgs>? DataAvailable;

    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;

            _notificationClient = _enumerator.CreateNotificationClient();
            _notificationClient.DefaultDeviceChanged += OnDefaultDeviceChanged;

            StartCaptureOnDefaultDevice();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_started)
            {
                return;
            }

            _started = false;

            if (_notificationClient is not null)
            {
                _notificationClient.DefaultDeviceChanged -= OnDefaultDeviceChanged;
                _notificationClient.Dispose();
                _notificationClient = null;
            }

            StopCaptureLocked();
        }
    }

    private void OnDefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
    {
        // 再生用（Render）の既定デバイス変更にのみ追従する。
        if (e.Flow != DataFlow.Render)
        {
            return;
        }

        lock (_gate)
        {
            if (!_started)
            {
                return;
            }

            _logger.LogInformation("既定の再生デバイスが変更されたため、音量メーターのキャプチャ対象を切り替えます。");
            StopCaptureLocked();
            StartCaptureOnDefaultDevice();
        }
    }

    private void StartCaptureOnDefaultDevice()
    {
        try
        {
            if (!_enumerator.TryGetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var device))
            {
                _logger.LogWarning("既定の再生デバイスが見つからないため、音量メーターのキャプチャを開始できません。");
                return;
            }

            var recorder = new WasapiRecorderBuilder()
                .WithDevice(device)
                .WithLoopbackCapture()
                .Build();

            recorder.DataAvailable += OnDataAvailable;

            _sampleRate = recorder.WaveFormat.SampleRate > 0 ? recorder.WaveFormat.SampleRate : 44100;
            UpdateFftSizeForSampleRate(_sampleRate);
            _fftWritePos = 0;
            _fftFilledCount = 0;
            _fftSamplesSinceLastCompute = 0;

            recorder.StartRecording();

            _recorder = recorder;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "既定の再生デバイスのループバックキャプチャ開始に失敗しました。");
        }
    }

    private void StopCaptureLocked()
    {
        if (_recorder is null)
        {
            return;
        }

        _recorder.DataAvailable -= OnDataAvailable;
        try
        {
            _recorder.StopRecording();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ループバックキャプチャの停止に失敗しました。");
        }

        _recorder.Dispose();
        _recorder = null;
    }

    private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        var recorder = _recorder;
        if (recorder is null || buffer.IsEmpty)
        {
            return;
        }

        var format = recorder.WaveFormat;
        var channels = Math.Max(1, format.Channels);

        // WASAPI の既定フォーマットは WAVEFORMATEXTENSIBLE で返されることが多く、
        // 実際のサンプル形式（PCM / IEEE Float）と有効ビット数は SubFormat 側に格納されている。
        var encoding = format.Encoding;
        var bitsPerSample = format.BitsPerSample;
        if (format is WaveFormatExtensible extensible)
        {
            var standard = extensible.ToStandardWaveFormat();
            encoding = standard.Encoding;
            bitsPerSample = extensible.ValidBitsPerSample > 0 ? extensible.ValidBitsPerSample : standard.BitsPerSample;
        }

        float leftPeak = 0f;
        float rightPeak = 0f;
        var sampleCount = 0;

        if (encoding == WaveFormatEncoding.IeeeFloat)
        {
            switch (bitsPerSample)
            {
                case 32:
                    {
                        var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(buffer);
                        for (var i = 0; i + channels <= samples.Length; i += channels)
                        {
                            var rawL = samples[i];
                            var rawR = channels > 1 ? samples[i + 1] : rawL;
                            var l = Math.Abs(rawL);
                            var r = Math.Abs(rawR);
                            if (l > leftPeak) leftPeak = l;
                            if (r > rightPeak) rightPeak = r;
                            PushFftSample((rawL + rawR) * 0.5f);
                            sampleCount++;
                        }
                        break;
                    }
                case 64:
                    {
                        var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, double>(buffer);
                        for (var i = 0; i + channels <= samples.Length; i += channels)
                        {
                            var rawL = (float)samples[i];
                            var rawR = channels > 1 ? (float)samples[i + 1] : rawL;
                            var l = Math.Abs(rawL);
                            var r = Math.Abs(rawR);
                            if (l > leftPeak) leftPeak = l;
                            if (r > rightPeak) rightPeak = r;
                            PushFftSample((rawL + rawR) * 0.5f);
                            sampleCount++;
                        }
                        break;
                    }
                default:
                    _logger.LogWarning("未対応の IEEE Float ビット数です: {BitsPerSample}", bitsPerSample);
                    return;
            }
        }
        else if (encoding == WaveFormatEncoding.Pcm || encoding == WaveFormatEncoding.Extensible)
        {
            switch (bitsPerSample)
            {
                case 8:
                    {
                        // 8bit PCM は符号なし（中心値 128）。
                        for (var i = 0; i + channels <= buffer.Length; i += channels)
                        {
                            var rawL = (buffer[i] - 128) / 128f;
                            var rawR = channels > 1 ? (buffer[i + 1] - 128) / 128f : rawL;
                            var l = Math.Abs(rawL);
                            var r = Math.Abs(rawR);
                            if (l > leftPeak) leftPeak = l;
                            if (r > rightPeak) rightPeak = r;
                            PushFftSample((rawL + rawR) * 0.5f);
                            sampleCount++;
                        }
                        break;
                    }
                case 16:
                    {
                        var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(buffer);
                        for (var i = 0; i + channels <= samples.Length; i += channels)
                        {
                            var rawL = samples[i] / 32768f;
                            var rawR = channels > 1 ? samples[i + 1] / 32768f : rawL;
                            var l = Math.Abs(rawL);
                            var r = Math.Abs(rawR);
                            if (l > leftPeak) leftPeak = l;
                            if (r > rightPeak) rightPeak = r;
                            PushFftSample((rawL + rawR) * 0.5f);
                            sampleCount++;
                        }
                        break;
                    }
                case 24:
                    {
                        // 24bit PCM はパック形式（1 サンプル = 3 バイト）のため専用に読み取る。
                        var bytesPerFrame = 3 * channels;
                        for (var offset = 0; offset + bytesPerFrame <= buffer.Length; offset += bytesPerFrame)
                        {
                            var rawL = ReadInt24(buffer, offset) / 8388608f;
                            var rawR = channels > 1 ? ReadInt24(buffer, offset + 3) / 8388608f : rawL;
                            var l = Math.Abs(rawL);
                            var r = Math.Abs(rawR);
                            if (l > leftPeak) leftPeak = l;
                            if (r > rightPeak) rightPeak = r;
                            PushFftSample((rawL + rawR) * 0.5f);
                            sampleCount++;
                        }
                        break;
                    }
                case 32:
                    {
                        var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, int>(buffer);
                        for (var i = 0; i + channels <= samples.Length; i += channels)
                        {
                            var rawL = samples[i] / 2147483648f;
                            var rawR = channels > 1 ? samples[i + 1] / 2147483648f : rawL;
                            var l = Math.Abs(rawL);
                            var r = Math.Abs(rawR);
                            if (l > leftPeak) leftPeak = l;
                            if (r > rightPeak) rightPeak = r;
                            PushFftSample((rawL + rawR) * 0.5f);
                            sampleCount++;
                        }
                        break;
                    }
                default:
                    _logger.LogWarning("未対応の PCM ビット数です: {BitsPerSample}", bitsPerSample);
                    return;
            }
        }
        else
        {
            // 未対応フォーマットの場合はレベル更新をスキップする。
            _logger.LogWarning("未対応の音声エンコーディングです: {Encoding}", encoding);
            return;
        }

        if (sampleCount == 0)
        {
            return;
        }

        DataAvailable?.Invoke(this, new AudioCaptureDataEventArgs(
            Math.Clamp(leftPeak, 0f, 1f),
            Math.Clamp(rightPeak, 0f, 1f),
            _lastSpectrum));
    }

    private static int ReadInt24(ReadOnlySpan<byte> buffer, int offset)
    {
        var value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
        // 符号拡張（24bit → 32bit）。
        if ((value & 0x00800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }
        return value;
    }

    /// <summary>
    /// L/R をミックスしたモノラルサンプルを FFT 用のリングバッファへ書き込む。
    /// 十分なサンプル数が溜まったら FFT を計算し、<see cref="_lastSpectrum"/> を更新する。
    /// </summary>
    /// <summary>
    /// サンプルレートに応じて FFT サイズを決定する。基準（44.1kHz 付近）では <see cref="BaseFftSize"/> を用い、
    /// サンプルレートが高いほど低域の周波数分解能を維持するために FFT サイズを比例して拡大する。
    /// </summary>
    private void UpdateFftSizeForSampleRate(int sampleRate)
    {
        var ratio = (double)sampleRate / BaseSampleRate;
        var desired = BaseFftSize * ratio;

        var size = MinFftLength;
        while (size < desired && size < MaxFftLength)
        {
            size *= 2;
        }

        if (size == _fftSize)
        {
            return;
        }

        _fftSize = size;
        _fftPow = (int)Math.Log2(size);
        _fftCircularBuffer = new float[size];
        _fftWorkBuffer = new Complex[size];
        _fftWritePos = 0;
        _fftFilledCount = 0;
        _fftSamplesSinceLastCompute = 0;
    }

    private void PushFftSample(float sample)
    {
        var fftSize = _fftSize;
        _fftCircularBuffer[_fftWritePos] = sample;
        _fftWritePos = (_fftWritePos + 1) % fftSize;
        if (_fftFilledCount < fftSize)
        {
            _fftFilledCount++;
        }

        _fftSamplesSinceLastCompute++;

        // 描画負荷を抑えるため、バッファが満タンかつ一定間隔（FFTサイズの 1/4）ごとに再計算する。
        if (_fftFilledCount >= fftSize && _fftSamplesSinceLastCompute >= fftSize / 4)
        {
            _fftSamplesSinceLastCompute = 0;
            ComputeSpectrum();
        }
    }

    private void ComputeSpectrum()
    {
        var fftSize = _fftSize;

        // リングバッファを時系列順に並べ替えつつ、Hamming 窓を適用して複素配列へコピーする。
        // 窓関数の合計値（windowSum）は、窓によるゲイン損失を後段で補正するために保持しておく。
        var windowSum = 0.0;
        for (var i = 0; i < fftSize; i++)
        {
            var index = (_fftWritePos + i) % fftSize;
            var window = (float)FastFourierTransform.HammingWindow(i, fftSize);
            windowSum += window;
            _fftWorkBuffer[i].X = _fftCircularBuffer[index] * window;
            _fftWorkBuffer[i].Y = 0f;
        }

        FastFourierTransform.FFT(true, _fftPow, _fftWorkBuffer);

        // NAudio の FFT(forward=true) は内部で 1/FftSize にスケーリングするため、
        // 窓関数のゲイン損失（windowSum 分だけ振幅が小さくなる）と片側スペクトラム分を
        // 2 * FftSize / windowSum で補正し、0dBFS を基準とした振幅に揃える。
        var normalization = windowSum > 0 ? 2.0 * fftSize / windowSum : 1.0;

        var spectrum = new float[SpectrumBandCount];
        var binCount = fftSize / 2;
        var sampleRate = _sampleRate;

        // 可聴域を、SPAN系アナライザーに準じた固定の周波数境界（SpectrumBandBoundaries）で
        // SpectrumBandCount 個のバンドに分割する（対数均等割りではなく固定目盛りを使用）。
        var maxFrequency = sampleRate / 2f;

        for (var band = 0; band < SpectrumBandCount; band++)
        {
            var bandStartFreq = SpectrumBandBoundaries[band];
            var bandEndFreq = SpectrumBandBoundaries[band + 1];

            var binStart = Math.Max(1, (int)(bandStartFreq / maxFrequency * binCount));
            var binEnd = Math.Max(binStart + 1, (int)Math.Ceiling(bandEndFreq / maxFrequency * binCount));
            binEnd = Math.Min(binEnd, binCount);

            // バンド内で最も大きいビンの振幅をそのバンドの代表値として採用する（平均ではなく最大値）。
            // 平均を取ると、特に高域側のように1バンドに含まれるビン数が多い場合にピーク成分が
            // 薄まってしまい、実際より不自然に弱いレベルとして表示されるため。
            float maxMagnitude = 0f;
            for (var bin = binStart; bin < binEnd; bin++)
            {
                var re = _fftWorkBuffer[bin].X;
                var im = _fftWorkBuffer[bin].Y;
                var magnitude = MathF.Sqrt(re * re + im * im);
                if (magnitude > maxMagnitude)
                {
                    maxMagnitude = magnitude;
                }
            }

            spectrum[band] = (float)ToSpectrumScale(maxMagnitude * (float)normalization);
        }

        _lastSpectrum = spectrum;
    }

    /// <summary>
    /// FFT の振幅値を dB 変換したうえで、<see cref="SpectrumMinDecibels"/>〜0dB を 0.0〜1.0 に正規化する。
    /// </summary>
    private static double ToSpectrumScale(float magnitude)
    {
        if (magnitude <= 0f)
        {
            return 0.0;
        }

        var decibels = 20.0 * Math.Log10(magnitude);
        if (decibels <= SpectrumMinDecibels)
        {
            return 0.0;
        }

        return Math.Clamp((decibels - SpectrumMinDecibels) / -SpectrumMinDecibels, 0.0, 1.0);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _enumerator.Dispose();
        _disposed = true;
    }
}

