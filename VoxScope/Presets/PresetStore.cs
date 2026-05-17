using System.IO;
using System.Text.Json;

namespace VoxScope.Presets;

public sealed class PresetStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _presetFilePath;

    public PresetStore()
        : this(GetDefaultPresetFilePath())
    {
    }

    internal PresetStore(string presetFilePath)
    {
        _presetFilePath = presetFilePath;
    }

    public IReadOnlyList<Preset> Load()
    {
        if (!File.Exists(_presetFilePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_presetFilePath);
            return JsonSerializer.Deserialize<List<Preset>>(json, SerializerOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void Save(IEnumerable<Preset> presets)
    {
        var directoryPath = Path.GetDirectoryName(_presetFilePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var orderedPresets = presets
            .OrderBy(preset => preset.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var json = JsonSerializer.Serialize(orderedPresets, SerializerOptions);
        File.WriteAllText(_presetFilePath, json);
    }

    private static string GetDefaultPresetFilePath()
    {
        var applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(applicationDataPath, "VoxScope", "presets.json");
    }
}
