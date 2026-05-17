using NAudio.Wave;

namespace VoxScope.Audio;

public sealed class AnalyzingSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _sourceProvider;
    private readonly AudioAnalyzer _audioAnalyzer;

    public AnalyzingSampleProvider(ISampleProvider sourceProvider, AudioAnalyzer audioAnalyzer)
    {
        _sourceProvider = sourceProvider;
        _audioAnalyzer = audioAnalyzer;
    }

    public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var samplesRead = _sourceProvider.Read(buffer, offset, count);
        _audioAnalyzer.AddOutputSamples(buffer, offset, samplesRead);
        return samplesRead;
    }
}
