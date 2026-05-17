using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoxScope.Effects;

public sealed class PitchShiftEffect : IAudioEffect
{
    private float _semitones;
    private SmbPitchShiftingSampleProvider? _provider;

    public float Semitones
    {
        get => _semitones;
        set
        {
            _semitones = Math.Clamp(value, -12f, 12f);
            UpdatePitchFactor();
        }
    }

    public float PitchFactor => MathF.Pow(2f, _semitones / 12f);

    public ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider)
    {
        _provider = new SmbPitchShiftingSampleProvider(sourceProvider);
        UpdatePitchFactor();
        return _provider;
    }

    private void UpdatePitchFactor()
    {
        if (_provider is not null)
        {
            _provider.PitchFactor = PitchFactor;
        }
    }
}
