using Microsoft.Win32;
using NLog;

namespace BluetoothBatteryMonitor.Utilities;

/// <summary>
/// Registers or removes the application from Windows startup via the
/// HKEY_CURRENT_USER Run registry key — no elevated privileges required.
/// </summary>
public static class AutoStartManager
{
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    private const string RegistryKeyPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    private const string AppName = "BluetoothBatteryMonitor";

    /// <summary>
    /// Adds or removes the auto-start registry entry to match <paramref name="enable"/>.
    /// Silently skips if the registry is not writable.
    /// </summary>
    public static void Apply(bool enable)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);

            if (key is null)
            {
                Log.Warn("Could not open registry key {Key} for writing.", RegistryKeyPath);
                return;
            }

            string exePath = $"\"{Environment.ProcessPath}\"";

            if (enable)
            {
                key.SetValue(AppName, exePath);
                Log.Info("Auto-start registry entry set: {Path}", exePath);
            }
            else
            {
                if (key.GetValue(AppName) is not null)
                {
                    key.DeleteValue(AppName, throwOnMissingValue: false);
                    Log.Info("Auto-start registry entry removed.");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update auto-start registry entry.");
        }
    }

    /// <summary>Returns <c>true</c> if the auto-start registry entry currently exists.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
            return key?.GetValue(AppName) is not null;
        }
        catch
        {
            return false;
        }
    }
}
