using NAudio.Dsp;
using NAudio.Wave;

namespace VoxScope.Effects;

public sealed class FormantShiftEffect : IAudioEffect
{
    private static readonly float[] ReferenceFrequenciesHz = [700f, 1200f, 2600f];
    private static readonly float[] QValues = [0.8f, 0.9f, 1.1f];
    private int _settingsVersion;
    private float _semitones;

    public float Semitones
    {
        get => _semitones;
        set
        {
            _semitones = Math.Clamp(value, -6f, 6f);
            Interlocked.Increment(ref _settingsVersion);
        }
    }

    public ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider)
    {
        return new FormantShiftSampleProvider(sourceProvider, this);
    }

    private sealed class FormantShiftSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _sourceProvider;
        private readonly FormantShiftEffect _effect;
        private readonly int _channels;
        private BiQuadFilter[][] _cutFilters;
        private BiQuadFilter[][] _boostFilters;
        private int _appliedSettingsVersion = -1;

        public FormantShiftSampleProvider(ISampleProvider sourceProvider, FormantShiftEffect effect)
        {
            _sourceProvider = sourceProvider;
            _effect = effect;
            _channels = sourceProvider.WaveFormat.Channels;
            _cutFilters = CreateFilterMatrix(_channels);
            _boostFilters = CreateFilterMatrix(_channels);
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

            if (Math.Abs(_effect.Semitones) < 0.01f)
            {
                return samplesRead;
            }

            for (var sampleOffset = 0; sampleOffset < samplesRead; sampleOffset++)
            {
                var channel = sampleOffset % _channels;
                var sample = buffer[offset + sampleOffset];

                for (var band = 0; band < ReferenceFrequenciesHz.Length; band++)
                {
                    sample = _cutFilters[channel][band].Transform(sample);
                    sample = _boostFilters[channel][band].Transform(sample);
                }

                buffer[offset + sampleOffset] = sample;
            }

            return samplesRead;
        }

        private void RebuildFilters()
        {
            var sampleRate = WaveFormat.SampleRate;
            var semitones = _effect.Semitones;
            var shiftRatio = MathF.Pow(2f, semitones / 12f);
            var strengthDb = MathF.Abs(semitones);
            var maximumFrequency = sampleRate * 0.45f;

            for (var channel = 0; channel < _channels; channel++)
            {
                for (var band = 0; band < ReferenceFrequenciesHz.Length; band++)
                {
                    var referenceFrequency = ReferenceFrequenciesHz[band];
                    var shiftedFrequency = Math.Clamp(referenceFrequency * shiftRatio, 80f, maximumFrequency);
                    var q = QValues[band];

                    _cutFilters[channel][band] = BiQuadFilter.PeakingEQ(
                        sampleRate,
                        referenceFrequency,
                        q,
                        -strengthDb);

                    _boostFilters[channel][band] = BiQuadFilter.PeakingEQ(
                        sampleRate,
                        shiftedFrequency,
                        q,
                        strengthDb);
                }
            }

            _appliedSettingsVersion = Volatile.Read(ref _effect._settingsVersion);
        }

        private static BiQuadFilter[][] CreateFilterMatrix(int channels)
        {
            var filters = new BiQuadFilter[channels][];

            for (var channel = 0; channel < channels; channel++)
            {
                filters[channel] = new BiQuadFilter[ReferenceFrequenciesHz.Length];
            }

            return filters;
        }
    }
}
