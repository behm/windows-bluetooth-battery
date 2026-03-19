using System.Threading;
using System.Windows;
using BluetoothBatteryMonitor.Models;
using BluetoothBatteryMonitor.Services;
using BluetoothBatteryMonitor.UI;
using NLog;

// Disambiguate from System.Windows.Forms.Application
using Application = System.Windows.Application;

namespace BluetoothBatteryMonitor.Utilities;

/// <summary>
/// Orchestrates application startup and shutdown:
/// initialises logging, config, Bluetooth polling, and the tray icon.
/// </summary>
public class AppBootstrap : IDisposable
{
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    private readonly ConfigManager _configManager;
    private readonly BluetoothService _bluetoothService;
    private TrayIconManager? _trayIconManager;

    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;
    private bool _disposed;

    // Track devices for low-battery notification debouncing (device id → alerted)
    private readonly HashSet<string> _lowBatteryAlerted = new();

    public AppBootstrap()
    {
        _configManager = new ConfigManager();
        _bluetoothService = new BluetoothService();
    }

    /// <summary>Called from App.OnStartup to wire everything up.</summary>
    public void Initialize()
    {
        LoggingSetup.Configure(_configManager.Config.LogLevel);
        Log.Info("BluetoothBatteryMonitor starting up.");

        _trayIconManager = new TrayIconManager(_configManager.Config.LowBatteryWarningThreshold);
        _trayIconManager.SettingsRequested += OnSettingsRequested;
        _trayIconManager.RefreshRequested += OnRefreshRequested;
        _trayIconManager.ExitRequested += OnExitRequested;

        AutoStartManager.Apply(_configManager.Config.AutoStart);

        _bluetoothService.DevicesUpdated += OnDevicesUpdated;

        StartPolling();

        Log.Info("Application initialised. Refresh interval: {Interval}s",
            _configManager.Config.RefreshIntervalSeconds);
    }

    private void StartPolling()
    {
        _pollCts = new CancellationTokenSource();
        // Run on a thread-pool thread so WinRT Bluetooth async operations are never
        // constrained by the WPF dispatcher's SynchronizationContext.  Without this,
        // WinRT COM continuations are marshalled back through the WPF dispatcher,
        // which stalls the entire discovery chain.
        _pollTask = Task.Run(() => RunPollingLoopAsync(_pollCts.Token));
    }

    private async Task RunPollingLoopAsync(CancellationToken ct)
    {
        // Perform an immediate first refresh, then repeat on the configured interval.
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _bluetoothService.RefreshAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled error during battery refresh.");
            }

            int intervalMs = _configManager.Config.RefreshIntervalSeconds * 1000;
            try
            {
                await Task.Delay(intervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Log.Debug("Polling loop exited.");
    }

    private void OnDevicesUpdated(object? sender, IReadOnlyList<BluetoothDeviceInfo> devices)
    {
        // BeginInvoke (async/fire-and-forget) is used intentionally here.
        // DevicesUpdated fires from a thread-pool thread (inside Task.Run), and
        // Dispatcher.Invoke would block that thread until the dispatcher processes
        // the work item, creating a deadlock risk under any dispatcher contention.
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _trayIconManager?.UpdateDevices(devices);
            CheckLowBatteryAlerts(devices);
        });
    }

    private void CheckLowBatteryAlerts(IReadOnlyList<BluetoothDeviceInfo> devices)
    {
        int threshold = _configManager.Config.LowBatteryWarningThreshold;

        foreach (BluetoothDeviceInfo device in devices)
        {
            bool isLow = device.BatteryPercent.HasValue && device.BatteryPercent.Value <= threshold;

            if (isLow && _lowBatteryAlerted.Add(device.DeviceId))
            {
                _trayIconManager?.ShowLowBatteryNotification(device);
            }
            else if (!isLow)
            {
                // Reset alert once battery is above threshold again
                _lowBatteryAlerted.Remove(device.DeviceId);
            }
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var settingsWindow = new SettingsWindow(_configManager);
            settingsWindow.ShowDialog();

            // Re-apply auto-start and restart polling if the interval changed
            AutoStartManager.Apply(_configManager.Config.AutoStart);
            RestartPolling();

            Log.Info("Settings applied.");
        });
    }

    private void OnRefreshRequested(object? sender, EventArgs e)
    {
        Log.Info("Manual refresh requested.");
        _ = _bluetoothService.RefreshAsync();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        Log.Info("Exit requested by user.");
        Application.Current?.Shutdown();
    }

    private void RestartPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        StartPolling();
    }

    /// <summary>Called from App.OnExit to clean up resources.</summary>
    public void Shutdown()
    {
        Log.Info("BluetoothBatteryMonitor shutting down.");
        _pollCts?.Cancel();

        try { _pollTask?.Wait(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }

        Dispose();
        LogManager.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _bluetoothService.DevicesUpdated -= OnDevicesUpdated;

        if (_trayIconManager is not null)
        {
            _trayIconManager.SettingsRequested -= OnSettingsRequested;
            _trayIconManager.RefreshRequested -= OnRefreshRequested;
            _trayIconManager.ExitRequested -= OnExitRequested;
            _trayIconManager.Dispose();
        }

        _bluetoothService.Dispose();
        _pollCts?.Dispose();
    }
}
