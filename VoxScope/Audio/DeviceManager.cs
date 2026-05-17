using NAudio.Wave;

namespace VoxScope.Audio;

public sealed class DeviceManager
{
    private static readonly AudioFormatCandidate[] PreferredFormats =
    [
        new(SupportedWaveFormat.WAVE_FORMAT_48M16, 48000, 1),
        new(SupportedWaveFormat.WAVE_FORMAT_44M16, 44100, 1),
        new(SupportedWaveFormat.WAVE_FORMAT_48S16, 48000, 2),
        new(SupportedWaveFormat.WAVE_FORMAT_44S16, 44100, 2),
    ];

    public IReadOnlyList<AudioDevice> GetInputDevices()
    {
        return Enumerable.Range(0, WaveInEvent.DeviceCount)
            .Select(deviceNumber =>
            {
                var capabilities = WaveInEvent.GetCapabilities(deviceNumber);
                return new AudioDevice(deviceNumber, capabilities.ProductName, capabilities.Channels);
            })
            .ToArray();
    }

    public IReadOnlyList<AudioDevice> GetOutputDevices()
    {
        return Enumerable.Range(0, WaveOut.DeviceCount)
            .Select(deviceNumber =>
            {
                var capabilities = WaveOut.GetCapabilities(deviceNumber);
                return new AudioDevice(deviceNumber, capabilities.ProductName, capabilities.Channels);
            })
            .ToArray();
    }

    public WaveFormat GetPreferredSharedFormat(int inputDeviceNumber, int outputDeviceNumber)
    {
        var inputCapabilities = WaveInEvent.GetCapabilities(inputDeviceNumber);
        var outputCapabilities = WaveOut.GetCapabilities(outputDeviceNumber);

        foreach (var candidate in PreferredFormats)
        {
            if (inputCapabilities.SupportsWaveFormat(candidate.SupportedWaveFormat)
                && outputCapabilities.SupportsWaveFormat(candidate.SupportedWaveFormat))
            {
                return new WaveFormat(candidate.SampleRate, 16, candidate.Channels);
            }
        }

        throw new InvalidOperationException("選択した入出力デバイスで共通利用できる PCM 16-bit フォーマットが見つかりません。");
    }

    private sealed record AudioFormatCandidate(
        SupportedWaveFormat SupportedWaveFormat,
        int SampleRate,
        int Channels);
}
