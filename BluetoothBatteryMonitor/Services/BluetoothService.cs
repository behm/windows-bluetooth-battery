using System.Collections.Concurrent;
using System.Diagnostics;

using BluetoothBatteryMonitor.Models;
using NLog;
using Windows.Devices.Enumeration;
using Windows.Devices.Enumeration.Pnp;

namespace BluetoothBatteryMonitor.Services;

/// <summary>
/// Discovers connected Bluetooth devices and reads their battery levels using the
/// same mechanism as the Windows "Bluetooth &amp; devices" settings page:
///
///   1. Enumerate Bluetooth Association Endpoint (AEP) devices and filter to those
///      that are currently connected (System.Devices.Aep.IsConnected).
///
///   2. For each connected device, look up PnP device nodes that belong to the same
///      device container and read DEVPKEY_Bluetooth_Battery_Percentage — the property
///      Windows populates from HFP (Hands-Free Profile) AT+IPHONEACCEV commands or
///      other driver-level battery reports.
///
/// Only devices that are actively connected are included. Paired-but-disconnected
/// devices are excluded.
/// </summary>
public class BluetoothService : IDisposable
{
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    // Bluetooth protocol GUID for Association Endpoint enumeration.
    private const string BluetoothProtocolId = "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}";

    // DEVPKEY_Bluetooth_Battery_Percentage — the PnP device property that Windows
    // populates from HFP AT commands or driver-reported battery data.
    private const string PnpBatteryPercentKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    // Association Endpoint properties requested during enumeration.
    private const string AepIsConnected = "System.Devices.Aep.IsConnected";
    private const string AepContainerId = "System.Devices.Aep.ContainerId";
    private const string AepDeviceAddress = "System.Devices.Aep.DeviceAddress";

    private readonly ConcurrentDictionary<string, BluetoothDeviceInfo> _devices = new();

    // Connected-device cache maintained by the DeviceWatcher.
    // Key = AEP device ID, Value = (Name, AddressKey, ContainerId).
    private readonly ConcurrentDictionary<string, (string Name, string AddressKey, Guid ContainerId)>
        _connectedAepDevices = new();

    private DeviceWatcher? _watcher;
    private readonly TaskCompletionSource _watcherReady = new();
    private bool _watcherEnumerationComplete;
    private bool _disposed;

    /// <summary>
    /// Raised whenever battery levels have been refreshed.
    /// The payload is a read-only snapshot of all currently known devices.
    /// </summary>
    public event EventHandler<IReadOnlyList<BluetoothDeviceInfo>>? DevicesUpdated;

    /// <summary>
    /// Raised when the <see cref="DeviceWatcher"/> detects a Bluetooth device
    /// connection or disconnection after the initial enumeration completes.
    /// </summary>
    public event EventHandler? DeviceConnectionChanged;

    /// <summary>
    /// Enumerates connected Bluetooth devices, reads battery levels from the PnP
    /// device tree, and returns the updated device list.
    /// </summary>
    public async Task<IReadOnlyList<BluetoothDeviceInfo>> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        Log.Debug("Starting Bluetooth device refresh.");

        _devices.Clear();

        // Wait for the watcher's initial enumeration so _connectedAepDevices is
        // populated, but respect the cancellation token.
        using var reg = cancellationToken.Register(() => _watcherReady.TrySetCanceled());
        try { await _watcherReady.Task; }
        catch (OperationCanceledException) { return _devices.Values.ToList().AsReadOnly(); }

        var connected = _connectedAepDevices.Values.ToList();
        Log.Info("{Count} connected Bluetooth device(s) from watcher cache.", connected.Count);

        await Task.WhenAll(connected.Select(d =>
            QueryDeviceBatteryFromCacheAsync(d.Name, d.AddressKey, d.ContainerId, cancellationToken)));

        var snapshot = _devices.Values.ToList().AsReadOnly();
        stopWatch.Stop();
        Log.Info("Refresh complete. {Count} device(s) with battery info.  Took {Elapsed}ms", snapshot.Count, stopWatch.ElapsedMilliseconds);
        DevicesUpdated?.Invoke(this, snapshot);
        return snapshot;
    }

    // -------------------------------------------------------------------------
    // Battery lookup via PnP device tree
    // -------------------------------------------------------------------------

    private async Task QueryDeviceBatteryFromCacheAsync(
        string name, string addressKey, Guid containerId, CancellationToken cancellationToken)
    {
        try
        {
            int? battery = await GetBatteryFromContainerAsync(containerId, cancellationToken);

            if (battery.HasValue)
            {
                Log.Info("Device '{Name}': battery = {Level}% (DEVPKEY_Bluetooth_Battery_Percentage)",
                    name, battery.Value);
                UpsertDevice(addressKey, name, battery.Value);
            }
            else
            {
                Log.Debug("Device '{Name}': no battery level found in PnP device tree.", name);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Error querying cached device '{Name}'.", name);
        }
    }

    /// <summary>
    /// Queries PnP device nodes within the given device container for
    /// DEVPKEY_Bluetooth_Battery_Percentage.
    /// </summary>
    private static async Task<int?> GetBatteryFromContainerAsync(
        Guid containerId, CancellationToken cancellationToken)
    {
        string containerFilter = $"System.Devices.ContainerId:=\"{{{containerId}}}\"";

        PnpObjectCollection pnpDevices;
        try
        {
            pnpDevices = await PnpObject.FindAllAsync(
                    PnpObjectType.Device,
                    [PnpBatteryPercentKey],
                    containerFilter)
                .AsTask(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PnpObject query failed for container {ContainerId}.", containerId);
            return null;
        }

        foreach (var pnpDevice in pnpDevices)
        {
            if (pnpDevice.Properties.TryGetValue(PnpBatteryPercentKey, out var val)
                && val is not null)
            {
                int? level = val switch
                {
                    byte b => b,
                    int i => i,
                    uint u => (int)u,
                    _ => null
                };

                if (level.HasValue)
                    return Math.Clamp(level.Value, 0, 100);
            }
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Real-time device watcher
    // -------------------------------------------------------------------------

    /// <summary>
    /// Starts a <see cref="DeviceWatcher"/> that monitors Bluetooth AEP devices
    /// for connection/disconnection changes. Events that occur after the initial
    /// enumeration raise <see cref="DeviceConnectionChanged"/>.
    /// </summary>
    public void StartWatching()
    {
        if (_watcher != null) return;

        string selector = $"System.Devices.Aep.ProtocolId:=\"{BluetoothProtocolId}\"";
        string[] requestedProperties = [AepIsConnected, AepContainerId, AepDeviceAddress];

        _watcher = DeviceInformation.CreateWatcher(
            selector, requestedProperties, DeviceInformationKind.AssociationEndpoint);

        _watcher.Added += OnWatcherAdded;
        _watcher.Updated += OnWatcherUpdated;
        _watcher.Removed += OnWatcherRemoved;
        _watcher.EnumerationCompleted += OnWatcherEnumerationCompleted;

        _watcherEnumerationComplete = false;
        _watcher.Start();
        Log.Info("Bluetooth device watcher started.");
    }

    public void StopWatching()
    {
        if (_watcher == null) return;

        _watcher.Added -= OnWatcherAdded;
        _watcher.Updated -= OnWatcherUpdated;
        _watcher.Removed -= OnWatcherRemoved;
        _watcher.EnumerationCompleted -= OnWatcherEnumerationCompleted;

        if (_watcher.Status is DeviceWatcherStatus.Started
            or DeviceWatcherStatus.EnumerationCompleted)
        {
            _watcher.Stop();
        }

        _watcher = null;
        Log.Info("Bluetooth device watcher stopped.");
    }

    private void OnWatcherEnumerationCompleted(DeviceWatcher sender, object args)
    {
        _watcherEnumerationComplete = true;
        _watcherReady.TrySetResult();
        Log.Debug("DeviceWatcher: initial enumeration complete ({Count} connected device(s) cached), now monitoring changes.",
            _connectedAepDevices.Count);
    }

    private void OnWatcherAdded(DeviceWatcher sender, DeviceInformation args)
    {
        TrackIfConnected(args);
        if (!_watcherEnumerationComplete) return;
        Log.Debug("DeviceWatcher: device added — {Name}", args.Name);
        DeviceConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWatcherUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        if (args.Properties.TryGetValue(AepIsConnected, out var val))
        {
            if (val is true)
            {
                // Became connected — Updated only provides a delta, so fetch the
                // full DeviceInformation to populate the cache.
                Log.Debug("DeviceWatcher: device connected — {Id}", args.Id);
                _ = FetchAndCacheDeviceAsync(args.Id);
            }
            else
            {
                _connectedAepDevices.TryRemove(args.Id, out _);
                Log.Debug("DeviceWatcher: device disconnected — {Id}", args.Id);
            }

            if (_watcherEnumerationComplete)
                DeviceConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnWatcherRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        _connectedAepDevices.TryRemove(args.Id, out _);
        if (!_watcherEnumerationComplete) return;
        Log.Debug("DeviceWatcher: device removed — {Id}", args.Id);
        DeviceConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// If the device is currently connected and has a container ID, cache it.
    /// </summary>
    private void TrackIfConnected(DeviceInformation device)
    {
        bool isConnected = device.Properties.TryGetValue(AepIsConnected, out var v) && v is true;
        if (!isConnected)
        {
            _connectedAepDevices.TryRemove(device.Id, out _);
            return;
        }

        if (!device.Properties.TryGetValue(AepContainerId, out var containerObj)
            || containerObj is not Guid containerId)
            return;

        string addressKey = device.Properties.TryGetValue(AepDeviceAddress, out var addrObj)
            && addrObj is string addr
                ? addr
                : device.Id;

        _connectedAepDevices[device.Id] = (device.Name, addressKey, containerId);
    }

    /// <summary>
    /// Fetches full device info by AEP ID and caches it. Called when the watcher
    /// reports a device as newly connected via an Updated event (which only
    /// carries a property delta).
    /// </summary>
    private async Task FetchAndCacheDeviceAsync(string deviceId)
    {
        try
        {
            string[] requestedProperties = [AepIsConnected, AepContainerId, AepDeviceAddress];
            var device = await DeviceInformation.CreateFromIdAsync(
                deviceId, requestedProperties, DeviceInformationKind.AssociationEndpoint);

            TrackIfConnected(device);
            Log.Debug("DeviceWatcher: cached newly connected device '{Name}'.", device.Name);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "DeviceWatcher: failed to fetch device info for {Id}.", deviceId);
        }
    }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private void UpsertDevice(string addressKey, string name, int batteryPercent)
    {
        var info = _devices.GetOrAdd(addressKey, _ => new BluetoothDeviceInfo
        {
            DeviceId = addressKey,
            Name = name
        });

        info.BatteryPercent = batteryPercent;
        info.LastUpdated = DateTime.UtcNow;
        info.IsConnected = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopWatching();
        _devices.Clear();
    }
}

