using System.Collections.Concurrent;
using BluetoothBatteryMonitor.Models;
using NLog;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace BluetoothBatteryMonitor.Services;

/// <summary>
/// Discovers paired Bluetooth audio devices and reads their battery levels
/// via the standard GATT Battery Service (UUID 0x180F / characteristic 0x2A19).
/// </summary>
public class BluetoothService : IDisposable
{
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    // Standard GATT Battery Service and Battery Level characteristic UUIDs
    private static readonly Guid BatteryServiceUuid = new("0000180f-0000-1000-8000-00805f9b34fb");
    private static readonly Guid BatteryLevelCharacteristicUuid = new("00002a19-0000-1000-8000-00805f9b34fb");

    // Audio device class filters for Bluetooth LE device enumeration
    private static readonly string[] AudioDeviceSelectors = new[]
    {
        // Bluetooth LE devices that have the Battery Service
        GattDeviceService.GetDeviceSelectorFromUuid(BatteryServiceUuid)
    };

    private readonly ConcurrentDictionary<string, BluetoothDeviceInfo> _devices = new();
    private bool _disposed;

    /// <summary>
    /// Raised whenever battery levels have been refreshed.
    /// The payload is a snapshot of all currently known devices.
    /// </summary>
    public event EventHandler<IReadOnlyList<BluetoothDeviceInfo>>? DevicesUpdated;

    /// <summary>
    /// Enumerates paired Bluetooth LE audio devices and reads their battery levels.
    /// Returns immediately with the refreshed device list.
    /// </summary>
    public async Task<IReadOnlyList<BluetoothDeviceInfo>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        Log.Debug("Starting Bluetooth device refresh.");

        // Query all BLE devices that advertise the Battery Service
        string selector = GattDeviceService.GetDeviceSelectorFromUuid(BatteryServiceUuid);
        DeviceInformationCollection foundDevices;

        try
        {
            foundDevices = await DeviceInformation.FindAllAsync(selector)
                .AsTask(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Device enumeration cancelled.");
            return GetSnapshot();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to enumerate Bluetooth devices.");
            return GetSnapshot();
        }

        Log.Info("Found {Count} Bluetooth device(s) with Battery Service.", foundDevices.Count);

        var tasks = foundDevices
            .Select(d => QueryDeviceAsync(d, cancellationToken))
            .ToList();

        await Task.WhenAll(tasks);

        var snapshot = GetSnapshot();
        DevicesUpdated?.Invoke(this, snapshot);
        return snapshot;
    }

    private async Task QueryDeviceAsync(DeviceInformation deviceInfo, CancellationToken cancellationToken)
    {
        BluetoothLEDevice? bleDevice = null;
        GattDeviceService? batteryService = null;

        try
        {
            bleDevice = await BluetoothLEDevice.FromIdAsync(deviceInfo.Id)
                .AsTask(cancellationToken);

            if (bleDevice is null)
            {
                Log.Warn("Could not open BLE device: {Name} ({Id})", deviceInfo.Name, deviceInfo.Id);
                return;
            }

            GattDeviceServicesResult servicesResult =
                await bleDevice.GetGattServicesForUuidAsync(BatteryServiceUuid, BluetoothCacheMode.Uncached)
                    .AsTask(cancellationToken);

            if (servicesResult.Status != GattCommunicationStatus.Success
                || servicesResult.Services.Count == 0)
            {
                Log.Debug("Device {Name} has no accessible Battery Service (status: {Status}).",
                    bleDevice.Name, servicesResult.Status);
                return;
            }

            batteryService = servicesResult.Services[0];

            GattCharacteristicsResult charsResult =
                await batteryService.GetCharacteristicsForUuidAsync(
                    BatteryLevelCharacteristicUuid, BluetoothCacheMode.Uncached)
                    .AsTask(cancellationToken);

            if (charsResult.Status != GattCommunicationStatus.Success
                || charsResult.Characteristics.Count == 0)
            {
                Log.Debug("Device {Name}: Battery Level characteristic not found.", bleDevice.Name);
                return;
            }

            GattCharacteristic batteryChar = charsResult.Characteristics[0];
            GattReadResult readResult = await batteryChar.ReadValueAsync(BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken);

            if (readResult.Status != GattCommunicationStatus.Success)
            {
                Log.Warn("Device {Name}: Battery Level read failed (status: {Status}).",
                    bleDevice.Name, readResult.Status);
                return;
            }

            // Battery Level characteristic returns a single byte: 0–100
            byte batteryLevel = Windows.Storage.Streams.DataReader.FromBuffer(readResult.Value).ReadByte();
            batteryLevel = Math.Min(batteryLevel, (byte)100);

            var info = _devices.GetOrAdd(deviceInfo.Id, _ => new BluetoothDeviceInfo
            {
                DeviceId = deviceInfo.Id,
                Name = bleDevice.Name
            });

            info.BatteryPercent = batteryLevel;
            info.LastUpdated = DateTime.UtcNow;
            info.IsConnected = true;

            Log.Info("Device '{Name}': battery = {Battery}%", bleDevice.Name, batteryLevel);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Query cancelled for device {Id}.", deviceInfo.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error querying device {Name} ({Id}).",
                deviceInfo.Name, deviceInfo.Id);

            // Mark device as not connected if it was previously tracked
            if (_devices.TryGetValue(deviceInfo.Id, out BluetoothDeviceInfo? existing))
            {
                existing.IsConnected = false;
            }
        }
        finally
        {
            batteryService?.Dispose();
            bleDevice?.Dispose();
        }
    }

    private IReadOnlyList<BluetoothDeviceInfo> GetSnapshot() =>
        _devices.Values.ToList().AsReadOnly();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _devices.Clear();
    }
}
