using NAudio.Wave;

namespace VoxScope.Effects;

public sealed class CompressorEffect : IAudioEffect
{
    private bool _enabled;
    private float _thresholdDb = -18f;
    private float _ratio = 3f;
    private float _makeupGainDb;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public float ThresholdDb
    {
        get => _thresholdDb;
        set => _thresholdDb = Math.Clamp(value, -60f, 0f);
    }

    public float Ratio
    {
        get => _ratio;
        set => _ratio = Math.Clamp(value, 1f, 12f);
    }

    public float MakeupGainDb
    {
        get => _makeupGainDb;
        set => _makeupGainDb = Math.Clamp(value, 0f, 18f);
    }

    public ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider)
    {
        return new CompressorSampleProvider(sourceProvider, this);
    }

    private sealed class CompressorSampleProvider : ISampleProvider
    {
        private const float AttackSeconds = 0.01f;
        private const float ReleaseSeconds = 0.12f;
        private readonly ISampleProvider _sourceProvider;
        private readonly CompressorEffect _effect;
        private readonly float _attackCoefficient;
        private readonly float _releaseCoefficient;
        private float _currentGain = 1f;

        public CompressorSampleProvider(ISampleProvider sourceProvider, CompressorEffect effect)
        {
            _sourceProvider = sourceProvider;
            _effect = effect;
            _attackCoefficient = CalculateCoefficient(sourceProvider.WaveFormat.SampleRate, AttackSeconds);
            _releaseCoefficient = CalculateCoefficient(sourceProvider.WaveFormat.SampleRate, ReleaseSeconds);
        }

        public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesRead = _sourceProvider.Read(buffer, offset, count);

            if (!_effect.Enabled)
            {
                _currentGain = 1f;
                return samplesRead;
            }

            var thresholdDb = _effect.ThresholdDb;
            var ratio = _effect.Ratio;
            var makeupGainDb = _effect.MakeupGainDb;

            for (var sampleOffset = 0; sampleOffset < samplesRead; sampleOffset++)
            {
                var sample = buffer[offset + sampleOffset];
                var inputDb = ToDb(Math.Abs(sample));
                var outputDb = inputDb > thresholdDb
                    ? thresholdDb + ((inputDb - thresholdDb) / ratio)
                    : inputDb;
                var desiredGain = DbToLinear((outputDb - inputDb) + makeupGainDb);
                var coefficient = desiredGain < _currentGain
                    ? _attackCoefficient
                    : _releaseCoefficient;

                _currentGain = desiredGain + (coefficient * (_currentGain - desiredGain));
                buffer[offset + sampleOffset] = sample * _currentGain;
            }

            return samplesRead;
        }

        private static float CalculateCoefficient(int sampleRate, float timeSeconds)
        {
            return MathF.Exp(-1f / (sampleRate * timeSeconds));
        }

        private static float ToDb(float amplitude)
        {
            return 20f * MathF.Log10(MathF.Max(amplitude, 1e-5f));
        }

        private static float DbToLinear(float gainDb)
        {
            return MathF.Pow(10f, gainDb / 20f);
        }
    }
}
