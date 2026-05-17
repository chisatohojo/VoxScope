using VoxScope.Presets;

namespace VoxScope.Settings;

public sealed record AppSettings(
    int? InputDeviceNumber,
    string? InputDeviceName,
    int? OutputDeviceNumber,
    string? OutputDeviceName,
    int SelectedEffectTabIndex,
    Preset CurrentSettings)
{
    public static AppSettings Default { get; } = new(
        null,
        null,
        null,
        null,
        0,
        Preset.Default with { Name = "Last Session" });
}
