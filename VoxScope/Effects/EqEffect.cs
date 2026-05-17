using NAudio.Dsp;
using NAudio.Wave;

namespace VoxScope.Effects;

public sealed class EqEffect : IAudioEffect
{
    private int _settingsVersion;
    private float _lowGainDb;
    private float _midGainDb;
    private float _highGainDb;

    public float LowGainDb
    {
        get => _lowGainDb;
        set
        {
            _lowGainDb = Math.Clamp(value, -12f, 12f);
            Interlocked.Increment(ref _settingsVersion);
        }
    }

    public float MidGainDb
    {
        get => _midGainDb;
        set
        {
            _midGainDb = Math.Clamp(value, -12f, 12f);
            Interlocked.Increment(ref _settingsVersion);
        }
    }

    public float HighGainDb
    {
        get => _highGainDb;
        set
        {
            _highGainDb = Math.Clamp(value, -12f, 12f);
            Interlocked.Increment(ref _settingsVersion);
        }
    }

    public ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider)
    {
        return new EqSampleProvider(sourceProvider, this);
    }

    private sealed class EqSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _sourceProvider;
        private readonly EqEffect _effect;
        private readonly int _channels;
        private BiQuadFilter[] _lowFilters;
        private BiQuadFilter[] _midFilters;
        private BiQuadFilter[] _highFilters;
        private int _appliedSettingsVersion = -1;

        public EqSampleProvider(ISampleProvider sourceProvider, EqEffect effect)
        {
            _sourceProvider = sourceProvider;
            _effect = effect;
            _channels = sourceProvider.WaveFormat.Channels;
            _lowFilters = new BiQuadFilter[_channels];
            _midFilters = new BiQuadFilter[_channels];
            _highFilters = new BiQuadFilter[_channels];
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

            for (var sampleOffset = 0; sampleOffset < samplesRead; sampleOffset++)
            {
                var channel = sampleOffset % _channels;
                var sample = buffer[offset + sampleOffset];
                sample = _lowFilters[channel].Transform(sample);
                sample = _midFilters[channel].Transform(sample);
                sample = _highFilters[channel].Transform(sample);
                buffer[offset + sampleOffset] = sample;
            }

            return samplesRead;
        }

        private void RebuildFilters()
        {
            var sampleRate = WaveFormat.SampleRate;
            var lowGainDb = _effect.LowGainDb;
            var midGainDb = _effect.MidGainDb;
            var highGainDb = _effect.HighGainDb;

            for (var channel = 0; channel < _channels; channel++)
            {
                _lowFilters[channel] = BiQuadFilter.LowShelf(sampleRate, 180f, 0.8f, lowGainDb);
                _midFilters[channel] = BiQuadFilter.PeakingEQ(sampleRate, 1200f, 0.9f, midGainDb);
                _highFilters[channel] = BiQuadFilter.HighShelf(sampleRate, 5000f, 0.8f, highGainDb);
            }

            _appliedSettingsVersion = Volatile.Read(ref _effect._settingsVersion);
        }
    }
}
