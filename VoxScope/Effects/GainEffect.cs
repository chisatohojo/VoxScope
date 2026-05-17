using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoxScope.Effects;

public sealed class GainEffect : IAudioEffect
{
    private float _gainDb;
    private VolumeSampleProvider? _provider;

    public float GainDb
    {
        get => _gainDb;
        set
        {
            _gainDb = Math.Clamp(value, -24f, 12f);
            UpdateVolume();
        }
    }

    public ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider)
    {
        _provider = new VolumeSampleProvider(sourceProvider);
        UpdateVolume();
        return _provider;
    }

    private void UpdateVolume()
    {
        if (_provider is not null)
        {
            _provider.Volume = DbToLinear(_gainDb);
        }
    }

    private static float DbToLinear(float db)
    {
        return MathF.Pow(10f, db / 20f);
    }
}
