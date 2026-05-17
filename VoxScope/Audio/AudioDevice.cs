namespace VoxScope.Audio;

public sealed record AudioDevice(int DeviceNumber, string Name, int Channels)
{
    public string DisplayName => $"{DeviceNumber}: {Name}";
}
