using NAudio.Wave;

namespace VoxScope.Effects;

public sealed class PostPitchArtifactReducerEffect : IAudioEffect
{
    public bool Enabled { get; set; } = true;

    public ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider)
    {
        return new PostPitchArtifactReducerSampleProvider(sourceProvider, this);
    }

    private sealed class PostPitchArtifactReducerSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _sourceProvider;
        private readonly PostPitchArtifactReducerEffect _effect;

        public PostPitchArtifactReducerSampleProvider(
            ISampleProvider sourceProvider,
            PostPitchArtifactReducerEffect effect)
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
                return samplesRead;
            }

            // Correction stages will be added here so they remain separate from the user EQ.
            return samplesRead;
        }
    }
}
