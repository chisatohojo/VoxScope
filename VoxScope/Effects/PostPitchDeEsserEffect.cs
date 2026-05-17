using NAudio.Dsp;
using NAudio.Wave;

namespace VoxScope.Effects;

public sealed class PostPitchDeEsserEffect : IAudioEffect
{
    private const float MinimumReductionDb = 3f;
    private const float MaximumReductionDb = 9f;
    private bool _enabled = true;
    private float _maxReductionDb = 6f;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public float MaxReductionDb
    {
        get => _maxReductionDb;
        set => _maxReductionDb = Math.Clamp(value, MinimumReductionDb, MaximumReductionDb);
    }

    public ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider)
    {
        return new PostPitchDeEsserSampleProvider(sourceProvider, this);
    }

    private sealed class PostPitchDeEsserSampleProvider : ISampleProvider
    {
        private const float DetectorThresholdDb = -28f;
        private const float DetectorTimeSeconds = 0.012f;
        private const float AttackSeconds = 0.004f;
        private const float ReleaseSeconds = 0.12f;
        private readonly ISampleProvider _sourceProvider;
        private readonly PostPitchDeEsserEffect _effect;
        private readonly int _channels;
        private readonly BiQuadFilter[] _highPassFilters;
        private readonly BiQuadFilter[] _lowPassFilters;
        private readonly float[] _bandPowerEnvelopes;
        private readonly float[] _currentBandGains;
        private readonly float _detectorCoefficient;
        private readonly float _attackCoefficient;
        private readonly float _releaseCoefficient;

        public PostPitchDeEsserSampleProvider(
            ISampleProvider sourceProvider,
            PostPitchDeEsserEffect effect)
        {
            _sourceProvider = sourceProvider;
            _effect = effect;
            _channels = sourceProvider.WaveFormat.Channels;
            _highPassFilters = new BiQuadFilter[_channels];
            _lowPassFilters = new BiQuadFilter[_channels];
            _bandPowerEnvelopes = new float[_channels];
            _currentBandGains = Enumerable.Repeat(1f, _channels).ToArray();
            _detectorCoefficient = CalculateCoefficient(sourceProvider.WaveFormat.SampleRate, DetectorTimeSeconds);
            _attackCoefficient = CalculateCoefficient(sourceProvider.WaveFormat.SampleRate, AttackSeconds);
            _releaseCoefficient = CalculateCoefficient(sourceProvider.WaveFormat.SampleRate, ReleaseSeconds);
            BuildBandFilters();
        }

        public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesRead = _sourceProvider.Read(buffer, offset, count);

            for (var sampleOffset = 0; sampleOffset < samplesRead; sampleOffset++)
            {
                var channel = sampleOffset % _channels;
                var sample = buffer[offset + sampleOffset];
                var bandSample = _highPassFilters[channel].Transform(sample);
                bandSample = _lowPassFilters[channel].Transform(bandSample);

                UpdateBandEnvelope(channel, bandSample);

                if (!_effect.Enabled)
                {
                    _currentBandGains[channel] = 1f;
                    continue;
                }

                var desiredBandGain = CalculateDesiredBandGain(channel);
                var coefficient = desiredBandGain < _currentBandGains[channel]
                    ? _attackCoefficient
                    : _releaseCoefficient;

                _currentBandGains[channel] = desiredBandGain
                    + (coefficient * (_currentBandGains[channel] - desiredBandGain));

                buffer[offset + sampleOffset] = sample
                    + (bandSample * (_currentBandGains[channel] - 1f));
            }

            return samplesRead;
        }

        private void BuildBandFilters()
        {
            var sampleRate = WaveFormat.SampleRate;
            var maximumFrequency = sampleRate * 0.45f;
            var lowPassFrequency = MathF.Min(10000f, maximumFrequency);
            var highPassFrequency = MathF.Min(4000f, lowPassFrequency * 0.7f);

            for (var channel = 0; channel < _channels; channel++)
            {
                _highPassFilters[channel] = BiQuadFilter.HighPassFilter(sampleRate, highPassFrequency, 0.707f);
                _lowPassFilters[channel] = BiQuadFilter.LowPassFilter(sampleRate, lowPassFrequency, 0.707f);
            }
        }

        private void UpdateBandEnvelope(int channel, float bandSample)
        {
            var bandPower = bandSample * bandSample;
            _bandPowerEnvelopes[channel] = bandPower
                + (_detectorCoefficient * (_bandPowerEnvelopes[channel] - bandPower));
        }

        private float CalculateDesiredBandGain(int channel)
        {
            var bandRms = MathF.Sqrt(MathF.Max(_bandPowerEnvelopes[channel], 1e-10f));
            var bandRmsDb = ToDb(bandRms);
            var overThresholdDb = MathF.Max(0f, bandRmsDb - DetectorThresholdDb);
            var desiredReductionDb = MathF.Min(_effect.MaxReductionDb, overThresholdDb * 0.75f);
            return DbToLinear(-desiredReductionDb);
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
