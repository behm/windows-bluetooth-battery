namespace BluetoothBatteryMonitor.Models;

/// <summary>
/// Persisted application configuration stored in the JSON config file.
/// </summary>
public class AppConfig
{
    /// <summary>How often to poll Bluetooth device battery levels (in seconds). Default 5 minutes.</summary>
    public int RefreshIntervalSeconds { get; set; } = 300;

    /// <summary>Whether to register the application to launch on Windows startup.</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>Minimum battery percentage before a low-battery warning tooltip is shown.</summary>
    public int LowBatteryWarningThreshold { get; set; } = 20;

    /// <summary>Minimum battery percentage before a really low (critical) battery warning is shown.</summary>
    public int ReallyLowBatteryWarningThreshold { get; set; } = 5;

    /// <summary>NLog minimum log level: Trace, Debug, Info, Warn, Error, Off.</summary>
    public string LogLevel { get; set; } = "Info";
}
