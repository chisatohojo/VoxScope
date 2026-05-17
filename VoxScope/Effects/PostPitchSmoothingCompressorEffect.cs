using NAudio.Wave;

namespace VoxScope.Effects;

public sealed class PostPitchSmoothingCompressorEffect : IAudioEffect
{
    private bool _enabled = true;
    private float _strength = 0.35f;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public float Strength
    {
        get => _strength;
        set => _strength = Math.Clamp(value, 0f, 1f);
    }

    public ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider)
    {
        return new PostPitchSmoothingCompressorSampleProvider(sourceProvider, this);
    }

    private sealed class PostPitchSmoothingCompressorSampleProvider : ISampleProvider
    {
        private const float DetectorTimeSeconds = 0.02f;
        private const float AttackSeconds = 0.015f;
        private const float ReleaseSeconds = 0.18f;
        private const float ThresholdDb = -24f;
        private const float MaximumReductionDb = 4f;
        private readonly ISampleProvider _sourceProvider;
        private readonly PostPitchSmoothingCompressorEffect _effect;
        private readonly int _channels;
        private readonly float[] _powerEnvelopes;
        private readonly float[] _currentGains;
        private readonly float _detectorCoefficient;
        private readonly float _attackCoefficient;
        private readonly float _releaseCoefficient;

        public PostPitchSmoothingCompressorSampleProvider(
            ISampleProvider sourceProvider,
            PostPitchSmoothingCompressorEffect effect)
        {
            _sourceProvider = sourceProvider;
            _effect = effect;
            _channels = sourceProvider.WaveFormat.Channels;
            _powerEnvelopes = new float[_channels];
            _currentGains = Enumerable.Repeat(1f, _channels).ToArray();
            _detectorCoefficient = CalculateCoefficient(sourceProvider.WaveFormat.SampleRate, DetectorTimeSeconds);
            _attackCoefficient = CalculateCoefficient(sourceProvider.WaveFormat.SampleRate, AttackSeconds);
            _releaseCoefficient = CalculateCoefficient(sourceProvider.WaveFormat.SampleRate, ReleaseSeconds);
        }

        public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesRead = _sourceProvider.Read(buffer, offset, count);

            for (var sampleOffset = 0; sampleOffset < samplesRead; sampleOffset++)
            {
                var channel = sampleOffset % _channels;
                var sample = buffer[offset + sampleOffset];
                UpdateEnvelope(channel, sample);

                if (!_effect.Enabled || _effect.Strength <= 0f)
                {
                    _currentGains[channel] = 1f;
                    continue;
                }

                var desiredGain = CalculateDesiredGain(channel);
                var coefficient = desiredGain < _currentGains[channel]
                    ? _attackCoefficient
                    : _releaseCoefficient;

                _currentGains[channel] = desiredGain
                    + (coefficient * (_currentGains[channel] - desiredGain));

                buffer[offset + sampleOffset] = sample * _currentGains[channel];
            }

            return samplesRead;
        }

        private void UpdateEnvelope(int channel, float sample)
        {
            var power = sample * sample;
            _powerEnvelopes[channel] = power
                + (_detectorCoefficient * (_powerEnvelopes[channel] - power));
        }

        private float CalculateDesiredGain(int channel)
        {
            var rms = MathF.Sqrt(MathF.Max(_powerEnvelopes[channel], 1e-10f));
            var inputDb = ToDb(rms);

            if (inputDb <= ThresholdDb)
            {
                return 1f;
            }

            var ratio = 1f + (0.75f * _effect.Strength);
            var compressedDb = ThresholdDb + ((inputDb - ThresholdDb) / ratio);
            var reductionDb = MathF.Min(MaximumReductionDb * _effect.Strength, inputDb - compressedDb);
            return DbToLinear(-reductionDb);
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
