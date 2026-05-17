using NAudio.Dsp;
using NAudio.Wave;

namespace VoxScope.Effects;

internal sealed class HighBandEnergyProbeEffect : IAudioEffect
{
    private readonly HighBandEnergyReference _reference;

    public HighBandEnergyProbeEffect(HighBandEnergyReference reference)
    {
        _reference = reference;
    }

    public ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider)
    {
        return new HighBandEnergyProbeSampleProvider(sourceProvider, _reference);
    }

    private sealed class HighBandEnergyProbeSampleProvider : ISampleProvider
    {
        private const float DetectorTimeSeconds = 0.05f;
        private readonly ISampleProvider _sourceProvider;
        private readonly HighBandEnergyReference _reference;
        private readonly int _channels;
        private readonly BiQuadFilter[] _highPassFilters;
        private readonly BiQuadFilter[] _lowPassFilters;
        private readonly float _detectorCoefficient;
        private float _powerEnvelope;

        public HighBandEnergyProbeSampleProvider(
            ISampleProvider sourceProvider,
            HighBandEnergyReference reference)
        {
            _sourceProvider = sourceProvider;
            _reference = reference;
            _channels = sourceProvider.WaveFormat.Channels;
            _highPassFilters = new BiQuadFilter[_channels];
            _lowPassFilters = new BiQuadFilter[_channels];
            _detectorCoefficient = CalculateCoefficient(sourceProvider.WaveFormat.SampleRate, DetectorTimeSeconds);
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
            }

            _reference.Update(_powerEnvelope);
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
            _powerEnvelope = bandPower + (_detectorCoefficient * (_powerEnvelope - bandPower));
        }

        private static float CalculateCoefficient(int sampleRate, float timeSeconds)
        {
            return MathF.Exp(-1f / (sampleRate * timeSeconds));
        }
    }
}
