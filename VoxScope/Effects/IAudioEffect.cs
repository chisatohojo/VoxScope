using NAudio.Wave;

namespace VoxScope.Effects;

public interface IAudioEffect
{
    ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider);
}
