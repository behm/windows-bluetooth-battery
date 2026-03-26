namespace BluetoothBatteryMonitor.Models;

/// <summary>
/// Represents the type of Bluetooth audio device.
/// </summary>
public enum DeviceType
{
    /// <summary>Unknown or unrecognized device type.</summary>
    Unknown,
    /// <summary>Over-ear or on-ear headphones.</summary>
    Headphones,
    /// <summary>Wireless earbuds or in-ear headphones.</summary>
    Earbuds,
    /// <summary>Portable or desktop Bluetooth speakers.</summary>
    Speaker,
    /// <summary>Gaming headset with microphone.</summary>
    Headset,
    /// <summary>Computer mouse.</summary>
    Mouse,
    /// <summary>Computer keyboard.</summary>
    Keyboard
}

/// <summary>
/// Represents a discovered Bluetooth audio device and its current battery level.
/// </summary>
public class BluetoothDeviceInfo
{
    /// <summary>System device ID (used to re-query the device).</summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>Human-readable display name of the device.</summary>
    public string Name { get; init; } = "Unknown Device";

    /// <summary>
    /// Battery level as a percentage 0–100, or null if the device does not expose
    /// a GATT Battery Service or if the query failed.
    /// </summary>
    public int? BatteryPercent { get; set; }

    /// <summary>UTC timestamp when the battery level was last successfully read.</summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the device is currently reachable / connected.</summary>
    public bool IsConnected { get; set; }

    /// <summary>The detected type of the Bluetooth device.</summary>
    public DeviceType DeviceType { get; set; } = DeviceType.Unknown;

    /// <summary>Returns a display string such as "🎧 AirPods Pro: 85%".</summary>
    public string ToDisplayString()
    {
        string icon = DeviceType switch
        {
            DeviceType.Headphones => "🎧",
            DeviceType.Earbuds => "🎧",
            DeviceType.Speaker => "🔊",
            DeviceType.Headset => "🎮",
            DeviceType.Mouse => "🖱️",
            DeviceType.Keyboard => "⌨️",
            _ => ""
        };

        string batteryText = BatteryPercent.HasValue ? $"{BatteryPercent}%" : "N/A";
        string prefix = string.IsNullOrEmpty(icon) ? "" : $"{icon} ";
        return $"{prefix}{Name}: {batteryText}";
    }
}
