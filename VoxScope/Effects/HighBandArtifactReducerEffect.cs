using NAudio.Dsp;
using NAudio.Wave;

namespace VoxScope.Effects;

public sealed class HighBandArtifactReducerEffect : IAudioEffect
{
    private const float MinimumReductionDb = 3f;
    private const float MaximumReductionDb = 12f;
    private readonly HighBandEnergyReference _reference;
    private bool _enabled = true;
    private float _maxReductionDb = 6f;

    internal HighBandArtifactReducerEffect(HighBandEnergyReference reference)
    {
        _reference = reference;
    }

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
        return new HighBandArtifactReducerSampleProvider(sourceProvider, this, _reference);
    }

    private sealed class HighBandArtifactReducerSampleProvider : ISampleProvider
    {
        private const float DetectorTimeSeconds = 0.05f;
        private const float AttackSeconds = 0.005f;
        private const float ReleaseSeconds = 0.16f;
        private const float MinimumOutputLevelDb = -42f;
        private const float ExcessMarginDb = 1.5f;
        private readonly ISampleProvider _sourceProvider;
        private readonly HighBandArtifactReducerEffect _effect;
        private readonly HighBandEnergyReference _reference;
        private readonly int _channels;
        private readonly BiQuadFilter[] _highPassFilters;
        private readonly BiQuadFilter[] _lowPassFilters;
        private readonly float[] _currentBandGains;
        private readonly float _detectorCoefficient;
        private readonly float _attackCoefficient;
        private readonly float _releaseCoefficient;
        private float _outputPowerEnvelope;

        public HighBandArtifactReducerSampleProvider(
            ISampleProvider sourceProvider,
            HighBandArtifactReducerEffect effect,
            HighBandEnergyReference reference)
        {
            _sourceProvider = sourceProvider;
            _effect = effect;
            _reference = reference;
            _channels = sourceProvider.WaveFormat.Channels;
            _highPassFilters = new BiQuadFilter[_channels];
            _lowPassFilters = new BiQuadFilter[_channels];
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
                UpdateEnvelope(bandSample);

                if (!_effect.Enabled)
                {
                    _currentBandGains[channel] = 1f;
                    continue;
                }

                var desiredBandGain = CalculateDesiredBandGain();
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

        private void UpdateEnvelope(float bandSample)
        {
            var bandPower = bandSample * bandSample;
            _outputPowerEnvelope = bandPower
                + (_detectorCoefficient * (_outputPowerEnvelope - bandPower));
        }

        private float CalculateDesiredBandGain()
        {
            var inputRmsDb = PowerToDb(_reference.PowerEnvelope);
            var outputRmsDb = PowerToDb(_outputPowerEnvelope);

            if (outputRmsDb < MinimumOutputLevelDb)
            {
                return 1f;
            }

            var excessDb = outputRmsDb - inputRmsDb - ExcessMarginDb;

            if (excessDb <= 0f)
            {
                return 1f;
            }

            var reductionDb = MathF.Min(_effect.MaxReductionDb, excessDb * 0.85f);
            return DbToLinear(-reductionDb);
        }

        private static float CalculateCoefficient(int sampleRate, float timeSeconds)
        {
            return MathF.Exp(-1f / (sampleRate * timeSeconds));
        }

        private static float PowerToDb(float power)
        {
            return 10f * MathF.Log10(MathF.Max(power, 1e-10f));
        }

        private static float DbToLinear(float gainDb)
        {
            return MathF.Pow(10f, gainDb / 20f);
        }
    }
}
