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
        bool createdNew = false;

        try
        {
            _mutex = new Mutex(initiallyOwned: true,
                name: "BluetoothBatteryMonitor_SingleInstance",
                out createdNew);
        }
        catch (AbandonedMutexException)
        {
            // The mutex was abandoned; we now own it and can treat this as the first instance.
            createdNew = true;
        }
        catch
        {
            // Any failure to create or acquire the mutex should not crash the app.
            _mutex = null;
            return false;
        }
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
