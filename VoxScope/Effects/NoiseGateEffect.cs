using NAudio.Wave;

namespace VoxScope.Effects;

public sealed class NoiseGateEffect : IAudioEffect
{
    private int _isOpen = 1;

    public bool Enabled { get; set; }

    public float ThresholdDb { get; set; } = -45f;

    public float AttackMilliseconds { get; set; } = 5f;

    public float ReleaseMilliseconds { get; set; } = 120f;

    public bool IsOpen => Volatile.Read(ref _isOpen) == 1;

    public ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider)
    {
        return new NoiseGateSampleProvider(sourceProvider, this);
    }

    private sealed class NoiseGateSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _sourceProvider;
        private readonly NoiseGateEffect _effect;
        private float _gain = 1f;

        public NoiseGateSampleProvider(ISampleProvider sourceProvider, NoiseGateEffect effect)
        {
            _sourceProvider = sourceProvider;
            _effect = effect;
        }

        public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesRead = _sourceProvider.Read(buffer, offset, count);

            if (!_effect.Enabled)
            {
                _gain = 1f;
                Volatile.Write(ref _effect._isOpen, 1);
                return samplesRead;
            }

            var channels = WaveFormat.Channels;
            var threshold = DbToLinear(_effect.ThresholdDb);
            var attackCoefficient = GetSmoothingCoefficient(_effect.AttackMilliseconds, WaveFormat.SampleRate);
            var releaseCoefficient = GetSmoothingCoefficient(_effect.ReleaseMilliseconds, WaveFormat.SampleRate);

            for (var frameOffset = 0; frameOffset < samplesRead; frameOffset += channels)
            {
                var framePeak = 0f;

                for (var channel = 0; channel < channels && frameOffset + channel < samplesRead; channel++)
                {
                    framePeak = MathF.Max(framePeak, MathF.Abs(buffer[offset + frameOffset + channel]));
                }

                var targetGain = framePeak >= threshold ? 1f : 0f;
                Volatile.Write(ref _effect._isOpen, targetGain > 0f ? 1 : 0);
                var coefficient = targetGain > _gain ? attackCoefficient : releaseCoefficient;
                _gain += (targetGain - _gain) * coefficient;

                for (var channel = 0; channel < channels && frameOffset + channel < samplesRead; channel++)
                {
                    buffer[offset + frameOffset + channel] *= _gain;
                }
            }

            return samplesRead;
        }

        private static float DbToLinear(float db)
        {
            return MathF.Pow(10f, db / 20f);
        }

        private static float GetSmoothingCoefficient(float milliseconds, int sampleRate)
        {
            if (milliseconds <= 0f)
            {
                return 1f;
            }

            var seconds = milliseconds / 1000f;
            return 1f - MathF.Exp(-1f / (seconds * sampleRate));
        }
    }
}
