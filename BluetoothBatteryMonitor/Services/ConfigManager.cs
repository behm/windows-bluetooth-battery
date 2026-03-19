using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BluetoothBatteryMonitor.Models;
using NLog;

namespace BluetoothBatteryMonitor.Services;

/// <summary>
/// Loads and saves application configuration to a JSON file in
/// %LOCALAPPDATA%\BluetoothBatteryMonitor\config.json.
/// </summary>
public class ConfigManager
{
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BluetoothBatteryMonitor");

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>Currently loaded configuration. Never null after construction.</summary>
    public AppConfig Config { get; private set; }

    public ConfigManager()
    {
        Config = Load();
    }

    /// <summary>
    /// Loads configuration from disk. Returns defaults if the file is missing or corrupt.
    /// </summary>
    private AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                Log.Info("Config file not found at {Path}. Using defaults.", ConfigFilePath);
                return new AppConfig();
            }

            string json = File.ReadAllText(ConfigFilePath);
            AppConfig? loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (loaded is null)
            {
                Log.Warn("Config file deserialized to null. Using defaults.");
                return new AppConfig();
            }

            Log.Info("Configuration loaded from {Path}", ConfigFilePath);
            return loaded;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load config from {Path}. Using defaults.", ConfigFilePath);
            return new AppConfig();
        }
    }

    /// <summary>
    /// Persists the current <see cref="Config"/> to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            string json = JsonSerializer.Serialize(Config, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
            Log.Info("Configuration saved to {Path}", ConfigFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save config to {Path}", ConfigFilePath);
        }
    }

    /// <summary>Replaces the current config with <paramref name="updated"/> and saves to disk.</summary>
    public void Update(AppConfig updated)
    {
        Config = updated;
        Save();
    }
}
