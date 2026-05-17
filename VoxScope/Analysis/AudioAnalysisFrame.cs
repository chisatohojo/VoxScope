namespace VoxScope.Analysis;

public sealed record AudioAnalysisFrame(
    IReadOnlyList<float> InputSpectrumDb,
    IReadOnlyList<float> OutputSpectrumDb,
    int SampleRate,
    double? InputPitchHz,
    double? InputAveragePitchHz,
    double? OutputPitchHz,
    double? OutputAveragePitchHz,
    double InputRmsDb,
    double InputPeakDb,
    double OutputRmsDb,
    double OutputPeakDb,
    bool GateOpen);
