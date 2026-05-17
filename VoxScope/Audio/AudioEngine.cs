using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using VoxScope.Effects;

namespace VoxScope.Audio;

public sealed class AudioEngine : IDisposable
{
    private static readonly AudioLatencyProfile DefaultLatencyProfile = new(
        InternalBufferMilliseconds: 500,
        CaptureBufferMilliseconds: 20,
        CaptureBufferCount: 3,
        PlaybackDesiredLatencyMilliseconds: 100,
        PlaybackBufferCount: 3);
    private readonly DeviceManager _deviceManager;
    private WaveInEvent? _capture;
    private WaveOutEvent? _playback;
    private BufferedWaveProvider? _buffer;

    public AudioEngine(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    public AudioEffectChain Effects { get; } = new();

    public AudioAnalyzer Analyzer { get; } = new();

    public bool IsRunning => _capture is not null && _playback is not null;

    public WaveFormat? CurrentWaveFormat { get; private set; }

    public int EstimatedLatencyMilliseconds
    {
        get
        {
            var buffer = _buffer;
            var waveFormat = CurrentWaveFormat;

            if (buffer is null || waveFormat is null)
            {
                return 0;
            }

            var queuedMilliseconds = buffer.BufferedBytes * 1000d / waveFormat.AverageBytesPerSecond;
            return (int)Math.Round(
                DefaultLatencyProfile.CaptureBufferMilliseconds
                + DefaultLatencyProfile.PlaybackDesiredLatencyMilliseconds
                + queuedMilliseconds);
        }
    }

    public void Start(AudioDevice inputDevice, AudioDevice outputDevice)
    {
        if (IsRunning)
        {
            return;
        }

        var waveFormat = _deviceManager.GetPreferredSharedFormat(
            inputDevice.DeviceNumber,
            outputDevice.DeviceNumber);

        var buffer = new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromMilliseconds(DefaultLatencyProfile.InternalBufferMilliseconds),
            DiscardOnBufferOverflow = true,
            ReadFully = true,
        };

        var capture = new WaveInEvent
        {
            DeviceNumber = inputDevice.DeviceNumber,
            WaveFormat = waveFormat,
            BufferMilliseconds = DefaultLatencyProfile.CaptureBufferMilliseconds,
            NumberOfBuffers = DefaultLatencyProfile.CaptureBufferCount,
        };

        var playback = new WaveOutEvent
        {
            DeviceNumber = outputDevice.DeviceNumber,
            DesiredLatency = DefaultLatencyProfile.PlaybackDesiredLatencyMilliseconds,
            NumberOfBuffers = DefaultLatencyProfile.PlaybackBufferCount,
        };

        try
        {
            var effectChain = Effects.Build(buffer.ToSampleProvider());
            var analyzedOutput = new AnalyzingSampleProvider(effectChain, Analyzer);
            capture.DataAvailable += OnDataAvailable;
            Analyzer.Start(
                waveFormat.SampleRate,
                waveFormat.Channels,
                () => Effects.NoiseGate.IsOpen,
                () => Effects.PitchShift.CurrentPitchFactor);
            playback.Init(analyzedOutput.ToWaveProvider16());
            playback.Play();
            capture.StartRecording();

            _buffer = buffer;
            _capture = capture;
            _playback = playback;
            CurrentWaveFormat = waveFormat;
        }
        catch
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.Dispose();
            playback.Dispose();
            throw;
        }
    }

    public void Stop()
    {
        var capture = _capture;
        var playback = _playback;
        var buffer = _buffer;

        _capture = null;
        _playback = null;
        _buffer = null;
        CurrentWaveFormat = null;
        Analyzer.Stop();

        if (capture is not null)
        {
            capture.DataAvailable -= OnDataAvailable;

            try
            {
                capture.StopRecording();
            }
            catch (InvalidOperationException)
            {
                // Already stopped by the underlying device.
            }

            capture.Dispose();
        }

        playback?.Stop();
        playback?.Dispose();
        buffer?.ClearBuffer();
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        Analyzer.AddInputSamples(args.Buffer, args.BytesRecorded);
        _buffer?.AddSamples(args.Buffer, 0, args.BytesRecorded);
    }

    private sealed record AudioLatencyProfile(
        int InternalBufferMilliseconds,
        int CaptureBufferMilliseconds,
        int CaptureBufferCount,
        int PlaybackDesiredLatencyMilliseconds,
        int PlaybackBufferCount);
}
