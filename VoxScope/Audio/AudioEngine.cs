using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using VoxScope.Effects;

namespace VoxScope.Audio;

public sealed class AudioEngine : IDisposable
{
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
            BufferDuration = TimeSpan.FromSeconds(2),
            DiscardOnBufferOverflow = true,
            ReadFully = true,
        };

        var capture = new WaveInEvent
        {
            DeviceNumber = inputDevice.DeviceNumber,
            WaveFormat = waveFormat,
            BufferMilliseconds = 20,
            NumberOfBuffers = 3,
        };

        var playback = new WaveOutEvent
        {
            DeviceNumber = outputDevice.DeviceNumber,
            DesiredLatency = 80,
            NumberOfBuffers = 3,
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
                () => Effects.PitchShift.PitchFactor);
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
}
