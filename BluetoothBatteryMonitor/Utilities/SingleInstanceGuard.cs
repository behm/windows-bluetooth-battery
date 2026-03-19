using System.Threading;

namespace BluetoothBatteryMonitor.Utilities;

/// <summary>
/// Ensures only one instance of the application runs at a time
/// using a named system Mutex.
/// </summary>
public static class SingleInstanceGuard
{
    private static Mutex? _mutex;

    /// <summary>
    /// Attempts to acquire the single-instance mutex.
    /// Returns <c>true</c> if this is the first instance; <c>false</c> if another is already running.
    /// </summary>
    public static bool Acquire()
    {
        _mutex = new Mutex(initiallyOwned: true,
            name: "Global\\BluetoothBatteryMonitor_SingleInstance",
            out bool createdNew);
        return createdNew;
    }

    /// <summary>Releases the mutex so a new instance can start.</summary>
    public static void Release()
    {
        if (_mutex is null) return;
        try { _mutex.ReleaseMutex(); } catch { /* already released */ }
        _mutex.Dispose();
        _mutex = null;
    }
}
