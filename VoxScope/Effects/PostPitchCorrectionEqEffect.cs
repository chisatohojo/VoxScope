using NAudio.Dsp;
using NAudio.Wave;

namespace VoxScope.Effects;

public sealed class PostPitchCorrectionEqEffect : IAudioEffect
{
    private int _settingsVersion;
    private bool _enabled = true;
    private float _strength = 0.35f;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            Interlocked.Increment(ref _settingsVersion);
        }
    }

    public float Strength
    {
        get => _strength;
        set
        {
            _strength = Math.Clamp(value, 0f, 1f);
            Interlocked.Increment(ref _settingsVersion);
        }
    }

    public ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider)
    {
        return new PostPitchCorrectionEqSampleProvider(sourceProvider, this);
    }

    private sealed class PostPitchCorrectionEqSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _sourceProvider;
        private readonly PostPitchCorrectionEqEffect _effect;
        private readonly int _channels;
        private BiQuadFilter[] _bodyFilters;
        private BiQuadFilter[] _presenceFilters;
        private BiQuadFilter[] _roughnessFilters;
        private int _appliedSettingsVersion = -1;

        public PostPitchCorrectionEqSampleProvider(
            ISampleProvider sourceProvider,
            PostPitchCorrectionEqEffect effect)
        {
            _sourceProvider = sourceProvider;
            _effect = effect;
            _channels = sourceProvider.WaveFormat.Channels;
            _bodyFilters = new BiQuadFilter[_channels];
            _presenceFilters = new BiQuadFilter[_channels];
            _roughnessFilters = new BiQuadFilter[_channels];
            RebuildFilters();
        }

        public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var settingsVersion = Volatile.Read(ref _effect._settingsVersion);

            if (_appliedSettingsVersion != settingsVersion)
            {
                RebuildFilters();
            }

            var samplesRead = _sourceProvider.Read(buffer, offset, count);

            if (!_effect.Enabled || _effect.Strength <= 0f)
            {
                return samplesRead;
            }

            for (var sampleOffset = 0; sampleOffset < samplesRead; sampleOffset++)
            {
                var channel = sampleOffset % _channels;
                var sample = buffer[offset + sampleOffset];
                sample = _bodyFilters[channel].Transform(sample);
                sample = _presenceFilters[channel].Transform(sample);
                sample = _roughnessFilters[channel].Transform(sample);
                buffer[offset + sampleOffset] = sample;
            }

            return samplesRead;
        }

        private void RebuildFilters()
        {
            var sampleRate = WaveFormat.SampleRate;
            var strength = _effect.Strength;

            for (var channel = 0; channel < _channels; channel++)
            {
                _bodyFilters[channel] = BiQuadFilter.PeakingEQ(sampleRate, 220f, 0.8f, 1.5f * strength);
                _presenceFilters[channel] = BiQuadFilter.PeakingEQ(sampleRate, 3500f, 0.9f, -3f * strength);
                _roughnessFilters[channel] = BiQuadFilter.HighShelf(sampleRate, 7000f, 0.8f, -2.5f * strength);
            }

            _appliedSettingsVersion = Volatile.Read(ref _effect._settingsVersion);
        }
    }
}
