using System.Collections.Concurrent;
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

    private bool _disposed;

    /// <summary>
    /// Raised whenever battery levels have been refreshed.
    /// The payload is a read-only snapshot of all currently known devices.
    /// </summary>
    public event EventHandler<IReadOnlyList<BluetoothDeviceInfo>>? DevicesUpdated;

    /// <summary>
    /// Enumerates connected Bluetooth devices, reads battery levels from the PnP
    /// device tree, and returns the updated device list.
    /// </summary>
    public async Task<IReadOnlyList<BluetoothDeviceInfo>> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        Log.Debug("Starting Bluetooth device refresh.");

        _devices.Clear();

        await DiscoverConnectedDevicesAsync(cancellationToken);

        var snapshot = _devices.Values.ToList().AsReadOnly();
        Log.Info("Refresh complete. {Count} device(s) with battery info.", snapshot.Count);
        DevicesUpdated?.Invoke(this, snapshot);
        return snapshot;
    }

    // -------------------------------------------------------------------------
    // Discovery: connected Bluetooth AEP devices
    // -------------------------------------------------------------------------

    private async Task DiscoverConnectedDevicesAsync(CancellationToken cancellationToken)
    {
        string selector = $"System.Devices.Aep.ProtocolId:=\"{BluetoothProtocolId}\"";
        string[] requestedProperties = [AepIsConnected, AepContainerId, AepDeviceAddress];

        DeviceInformationCollection allDevices;
        try
        {
            allDevices = await DeviceInformation.FindAllAsync(
                    selector, requestedProperties, DeviceInformationKind.AssociationEndpoint)
                .AsTask(cancellationToken);

            Log.Debug("Bluetooth AEP enumeration returned {Count} device(s).", allDevices.Count);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to enumerate Bluetooth AEP devices.");
            return;
        }

        var connectedDevices = allDevices
            .Where(d => d.Properties.TryGetValue(AepIsConnected, out var v) && v is true)
            .ToList();

        Log.Info("{Count} connected Bluetooth device(s) found.", connectedDevices.Count);

        foreach (var dev in connectedDevices)
        {
            Log.Debug("Connected: {Name} ({Id})", dev.Name, dev.Id);
        }

        await Task.WhenAll(connectedDevices.Select(
            d => QueryDeviceBatteryAsync(d, cancellationToken)));
    }

    // -------------------------------------------------------------------------
    // Battery lookup via PnP device tree
    // -------------------------------------------------------------------------

    private async Task QueryDeviceBatteryAsync(
        DeviceInformation device, CancellationToken cancellationToken)
    {
        try
        {
            string addressKey = device.Properties.TryGetValue(AepDeviceAddress, out var addrObj)
                && addrObj is string addr
                    ? addr
                    : device.Id;

            if (!device.Properties.TryGetValue(AepContainerId, out var containerObj)
                || containerObj is not Guid containerId)
            {
                Log.Debug("Device '{Name}': no ContainerId available.", device.Name);
                return;
            }

            int? battery = await GetBatteryFromContainerAsync(containerId, cancellationToken);

            if (battery.HasValue)
            {
                Log.Info("Device '{Name}': battery = {Level}% (DEVPKEY_Bluetooth_Battery_Percentage)",
                    device.Name, battery.Value);
                UpsertDevice(addressKey, device.Name, battery.Value);
            }
            else
            {
                Log.Debug("Device '{Name}': no battery level found in PnP device tree.",
                    device.Name);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Error querying device '{Name}' ({Id}).", device.Name, device.Id);
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
        _devices.Clear();
    }
}

