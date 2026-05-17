using NAudio.Wave;

namespace VoxScope.Effects;

public sealed class AudioEffectChain
{
    private readonly HighBandEnergyReference _highBandEnergyReference = new();
    private readonly HighBandEnergyProbeEffect _highBandEnergyProbe;

    public AudioEffectChain()
    {
        _highBandEnergyProbe = new HighBandEnergyProbeEffect(_highBandEnergyReference);
        HighBandArtifactReducer = new HighBandArtifactReducerEffect(_highBandEnergyReference);
    }

    public NoiseGateEffect NoiseGate { get; } = new();

    public PitchShiftEffect PitchShift { get; } = new();

    public FormantShiftEffect FormantShift { get; } = new();

    public HighBandArtifactReducerEffect HighBandArtifactReducer { get; }

    public PostPitchArtifactReducerEffect PostPitchArtifactReducer { get; } = new();

    public PostPitchCorrectionEqEffect PostPitchCorrectionEq { get; } = new();

    public PostPitchDeEsserEffect PostPitchDeEsser { get; } = new();

    public PostPitchSmoothingCompressorEffect PostPitchSmoothingCompressor { get; } = new();

    public EqEffect Eq { get; } = new();

    public CompressorEffect Compressor { get; } = new();

    public GainEffect Gain { get; } = new();

    public ISampleProvider Build(ISampleProvider sourceProvider)
    {
        ISampleProvider current = sourceProvider;
        current = NoiseGate.CreateSampleProvider(current);
        current = _highBandEnergyProbe.CreateSampleProvider(current);
        current = PitchShift.CreateSampleProvider(current);
        current = FormantShift.CreateSampleProvider(current);
        current = HighBandArtifactReducer.CreateSampleProvider(current);
        current = PostPitchArtifactReducer.CreateSampleProvider(current);
        current = PostPitchCorrectionEq.CreateSampleProvider(current);
        current = PostPitchDeEsser.CreateSampleProvider(current);
        current = PostPitchSmoothingCompressor.CreateSampleProvider(current);
        current = Eq.CreateSampleProvider(current);
        current = Compressor.CreateSampleProvider(current);
        current = Gain.CreateSampleProvider(current);
        return current;
    }
}
