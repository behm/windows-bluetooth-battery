namespace BluetoothBatteryMonitor.Models;

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

    /// <summary>Returns a display string such as "AirPods Pro: 85%".</summary>
    public string ToDisplayString()
    {
        string batteryText = BatteryPercent.HasValue ? $"{BatteryPercent}%" : "N/A";
        return $"{Name}: {batteryText}";
    }
}
