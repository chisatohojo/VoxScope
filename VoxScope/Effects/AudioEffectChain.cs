using NAudio.Wave;

namespace VoxScope.Effects;

public sealed class AudioEffectChain
{
    public NoiseGateEffect NoiseGate { get; } = new();

    public PitchShiftEffect PitchShift { get; } = new();

    public EqEffect Eq { get; } = new();

    public GainEffect Gain { get; } = new();

    public ISampleProvider Build(ISampleProvider sourceProvider)
    {
        ISampleProvider current = sourceProvider;
        current = NoiseGate.CreateSampleProvider(current);
        current = PitchShift.CreateSampleProvider(current);
        current = Eq.CreateSampleProvider(current);
        current = Gain.CreateSampleProvider(current);
        return current;
    }
}
