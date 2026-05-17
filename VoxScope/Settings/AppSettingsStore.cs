using System.IO;
using System.Text.Json;

namespace VoxScope.Settings;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsFilePath;

    public AppSettingsStore()
        : this(GetDefaultSettingsFilePath())
    {
    }

    internal AppSettingsStore(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
    }

    public AppSettings? Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(AppSettings settings)
    {
        var directoryPath = Path.GetDirectoryName(_settingsFilePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_settingsFilePath, json);
    }

    private static string GetDefaultSettingsFilePath()
    {
        var applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(applicationDataPath, "VoxScope", "settings.json");
    }
}
