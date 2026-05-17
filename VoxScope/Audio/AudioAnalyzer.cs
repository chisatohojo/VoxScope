using System.Threading;
using VoxScope.Analysis;

namespace VoxScope.Audio;

public sealed class AudioAnalyzer : IDisposable
{
    private const int RingBufferSize = 8192;
    private readonly object _syncRoot = new();
    private readonly float[] _inputRingBuffer = new float[RingBufferSize];
    private readonly float[] _outputRingBuffer = new float[RingBufferSize];
    private readonly Queue<double> _inputPitchHistory = new();
    private readonly FftAnalyzer _fftAnalyzer = new();
    private readonly PitchDetector _pitchDetector = new();
    private Timer? _timer;
    private int _sampleRate;
    private int _channels;
    private int _inputWriteIndex;
    private int _outputWriteIndex;
    private int _inputSamplesAvailable;
    private int _outputSamplesAvailable;
    private double _inputRmsDb = -96d;
    private double _inputPeakDb = -96d;
    private double _outputRmsDb = -96d;
    private double _outputPeakDb = -96d;
    private Func<bool>? _gateStateProvider;
    private Func<double>? _pitchFactorProvider;

    public event Action<AudioAnalysisFrame>? AnalysisUpdated;

    public void Start(int sampleRate, int channels, Func<bool> gateStateProvider, Func<double> pitchFactorProvider)
    {
        lock (_syncRoot)
        {
            Array.Clear(_inputRingBuffer);
            Array.Clear(_outputRingBuffer);
            _inputPitchHistory.Clear();
            _sampleRate = sampleRate;
            _channels = channels;
            _inputWriteIndex = 0;
            _outputWriteIndex = 0;
            _inputSamplesAvailable = 0;
            _outputSamplesAvailable = 0;
            _inputRmsDb = -96d;
            _inputPeakDb = -96d;
            _outputRmsDb = -96d;
            _outputPeakDb = -96d;
            _gateStateProvider = gateStateProvider;
            _pitchFactorProvider = pitchFactorProvider;
        }

        _timer?.Dispose();
        _timer = new Timer(_ => PublishFrame(), null, TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(80));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;

        lock (_syncRoot)
        {
            _inputPitchHistory.Clear();
            _inputSamplesAvailable = 0;
            _outputSamplesAvailable = 0;
            _gateStateProvider = null;
            _pitchFactorProvider = null;
        }
    }

    public void AddInputSamples(byte[] buffer, int bytesRecorded)
    {
        if (_sampleRate <= 0 || _channels <= 0 || bytesRecorded <= 0)
        {
            return;
        }

        var totalSamples = bytesRecorded / sizeof(short);
        var frames = totalSamples / _channels;

        if (frames <= 0)
        {
            return;
        }

        var sumSquares = 0d;
        var peak = 0d;

        lock (_syncRoot)
        {
            for (var frame = 0; frame < frames; frame++)
            {
                var monoSample = 0d;

                for (var channel = 0; channel < _channels; channel++)
                {
                    var sampleIndex = (frame * _channels) + channel;
                    var byteIndex = sampleIndex * sizeof(short);
                    var sampleValue = (short)(buffer[byteIndex] | (buffer[byteIndex + 1] << 8));
                    var normalizedSample = sampleValue / 32768f;
                    monoSample += normalizedSample;
                    sumSquares += normalizedSample * normalizedSample;
                    peak = Math.Max(peak, Math.Abs(normalizedSample));
                }

                _inputRingBuffer[_inputWriteIndex] = (float)(monoSample / _channels);
                _inputWriteIndex = (_inputWriteIndex + 1) % _inputRingBuffer.Length;
            }

            _inputSamplesAvailable = Math.Min(_inputSamplesAvailable + frames, _inputRingBuffer.Length);
            _inputRmsDb = ToDb(Math.Sqrt(sumSquares / totalSamples));
            _inputPeakDb = ToDb(peak);
        }
    }

    public void AddOutputSamples(float[] buffer, int offset, int count)
    {
        if (count <= 0)
        {
            return;
        }

        var sumSquares = 0d;
        var peak = 0d;

        lock (_syncRoot)
        {
            var frames = count / _channels;

            for (var frame = 0; frame < frames; frame++)
            {
                var monoSample = 0d;

                for (var channel = 0; channel < _channels; channel++)
                {
                    var sample = buffer[offset + (frame * _channels) + channel];
                    monoSample += sample;
                    sumSquares += sample * sample;
                    peak = Math.Max(peak, Math.Abs(sample));
                }

                _outputRingBuffer[_outputWriteIndex] = (float)(monoSample / _channels);
                _outputWriteIndex = (_outputWriteIndex + 1) % _outputRingBuffer.Length;
            }

            _outputSamplesAvailable = Math.Min(_outputSamplesAvailable + frames, _outputRingBuffer.Length);
            _outputRmsDb = ToDb(Math.Sqrt(sumSquares / count));
            _outputPeakDb = ToDb(peak);
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void PublishFrame()
    {
        float[] inputSamples;
        float[] outputSamples;
        int outputSamplesAvailable;
        int sampleRate;
        double inputRmsDb;
        double inputPeakDb;
        double outputRmsDb;
        double outputPeakDb;
        Func<bool>? gateStateProvider;
        Func<double>? pitchFactorProvider;

        lock (_syncRoot)
        {
            if (_sampleRate <= 0 || _inputSamplesAvailable == 0)
            {
                return;
            }

            inputSamples = CopyLatestSamples(_inputRingBuffer, _inputWriteIndex, _inputSamplesAvailable);
            outputSamples = CopyLatestSamples(_outputRingBuffer, _outputWriteIndex, _outputSamplesAvailable);
            outputSamplesAvailable = _outputSamplesAvailable;
            sampleRate = _sampleRate;
            inputRmsDb = _inputRmsDb;
            inputPeakDb = _inputPeakDb;
            outputRmsDb = _outputRmsDb;
            outputPeakDb = _outputPeakDb;
            gateStateProvider = _gateStateProvider;
            pitchFactorProvider = _pitchFactorProvider;
        }

        var inputSpectrum = _fftAnalyzer.Analyze(inputSamples);
        var outputSpectrum = outputSamplesAvailable == 0
            ? Array.Empty<float>()
            : _fftAnalyzer.Analyze(outputSamples);
        var inputPitch = _pitchDetector.Detect(inputSamples, sampleRate, 70d, 350d);
        var pitchFactor = pitchFactorProvider?.Invoke() ?? 1d;
        double? outputPitch = inputPitch is { } detectedPitch
            ? detectedPitch * pitchFactor
            : null;
        double? inputAveragePitch;
        double? outputAveragePitch;

        lock (_syncRoot)
        {
            if (inputPitch is { } detectedInputPitch)
            {
                _inputPitchHistory.Enqueue(detectedInputPitch);

                while (_inputPitchHistory.Count > 8)
                {
                    _inputPitchHistory.Dequeue();
                }
            }

            inputAveragePitch = _inputPitchHistory.Count == 0 ? null : _inputPitchHistory.Average();
            outputAveragePitch = inputAveragePitch is { } averagePitch
                ? averagePitch * pitchFactor
                : null;
        }

        AnalysisUpdated?.Invoke(
            new AudioAnalysisFrame(
                inputSpectrum,
                outputSpectrum,
                sampleRate,
                inputPitch,
                inputAveragePitch,
                outputPitch,
                outputAveragePitch,
                inputRmsDb,
                inputPeakDb,
                outputRmsDb,
                outputPeakDb,
                gateStateProvider?.Invoke() ?? true));
    }

    private static float[] CopyLatestSamples(float[] ringBuffer, int writeIndex, int samplesAvailable)
    {
        var samples = new float[FftAnalyzer.FftSize];
        var availableSamples = Math.Min(samplesAvailable, samples.Length);
        var destinationOffset = samples.Length - availableSamples;
        var sourceIndex = (writeIndex - availableSamples + ringBuffer.Length) % ringBuffer.Length;

        for (var index = 0; index < availableSamples; index++)
        {
            samples[destinationOffset + index] = ringBuffer[(sourceIndex + index) % ringBuffer.Length];
        }

        return samples;
    }

    private static double ToDb(double amplitude)
    {
        return 20d * Math.Log10(Math.Max(amplitude, 1e-5d));
    }
}
