using System.Collections.Concurrent;
using BluetoothBatteryMonitor.Models;
using NLog;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace BluetoothBatteryMonitor.Services;

/// <summary>
/// Discovers paired Bluetooth devices (Classic and LE) and reads their battery levels
/// using three layered strategies:
///
///   1. Enumerate all paired Classic Bluetooth devices and read the
///      "System.Devices.BatteryLife" Windows device property.  This is the most
///      reliable path for audio devices (headphones, earbuds, speakers) that connect
///      via Classic Bluetooth A2DP/HFP.
///
///   2. For Classic devices that do not expose the Windows property, attempt to open
///      a BLE connection to the same Bluetooth address and query the GATT Battery
///      Service (UUID 0x180F / characteristic 0x2A19).
///
///   3. Enumerate all BLE devices that explicitly advertise the GATT Battery Service,
///      catching any pure-BLE devices missed by the Classic path.
///
/// Devices are deduplicated by Bluetooth hardware address so a dual-mode device
/// (Classic + LE) is never reported twice.
/// </summary>
public class BluetoothService : IDisposable
{
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    private static readonly Guid BatteryServiceUuid =
        new("0000180f-0000-1000-8000-00805f9b34fb");

    private static readonly Guid BatteryLevelCharacteristicUuid =
        new("00002a19-0000-1000-8000-00805f9b34fb");

    // Windows device property that contains the battery level (0–100) for paired
    // Bluetooth devices whose drivers report it through the Windows device stack.
    private const string BatteryLifeProperty = "System.Devices.BatteryLife";

    // Keyed by Bluetooth hardware address (ulong as string) for deduplication across
    // Classic and LE discovery paths.
    private readonly ConcurrentDictionary<string, BluetoothDeviceInfo> _devices = new();

    private bool _disposed;

    /// <summary>
    /// Raised whenever battery levels have been refreshed.
    /// The payload is a read-only snapshot of all currently known devices.
    /// </summary>
    public event EventHandler<IReadOnlyList<BluetoothDeviceInfo>>? DevicesUpdated;

    /// <summary>
    /// Runs all discovery strategies concurrently and returns the updated device list.
    /// </summary>
    public async Task<IReadOnlyList<BluetoothDeviceInfo>> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        Log.Debug("Starting Bluetooth device refresh.");

        // Clear entries from the previous poll so that devices which have since
        // disconnected or been powered off are not included in this snapshot.
        _devices.Clear();

        // Strategies 1 & 2 run together; Strategy 3 is additive for pure-BLE devices.
        await Task.WhenAll(
            DiscoverClassicDevicesAsync(cancellationToken),
            DiscoverLeGattDevicesAsync(cancellationToken));

        var snapshot = GetSnapshot();
        Log.Info("Refresh complete. {Count} device(s) with battery info.", snapshot.Count);
        DevicesUpdated?.Invoke(this, snapshot);
        return snapshot;
    }

    // -------------------------------------------------------------------------
    // Strategy 1 + 2: Classic Bluetooth paired devices
    // -------------------------------------------------------------------------

    private async Task DiscoverClassicDevicesAsync(CancellationToken cancellationToken)
    {
        string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        DeviceInformationCollection classicDevices;

        try
        {
            classicDevices = await DeviceInformation
                .FindAllAsync(selector, new[] { BatteryLifeProperty })
                .AsTask(cancellationToken);

            Log.Debug("Found {Count} paired Classic Bluetooth device(s).", classicDevices.Count);
            foreach (var device in classicDevices)
            {
                Log.Debug("Paired device: {Name} ({Id})", device.Name, device.Id);
            }
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to enumerate Classic Bluetooth devices.");
            return;
        }

        Log.Info("Classic Bluetooth: {Count} paired device(s) found.", classicDevices.Count);

        await Task.WhenAll(classicDevices.Select(
            d => QueryClassicDeviceAsync(d, cancellationToken)));
    }

    private async Task QueryClassicDeviceAsync(
        DeviceInformation deviceInfo, CancellationToken cancellationToken)
    {
        BluetoothDevice? device = null;
        try
        {
            device = await BluetoothDevice.FromIdAsync(deviceInfo.Id)
                .AsTask(cancellationToken);

            if (device is null)
            {
                Log.Warn("Could not open Classic device: {Name} ({Id})",
                    deviceInfo.Name, deviceInfo.Id);
                return;
            }

            string addressKey = device.BluetoothAddress.ToString();

            // Strategy 1: Windows device property (populated by the Windows BT stack
            // for most paired audio peripherals — the fastest and most reliable path).
            if (deviceInfo.Properties.TryGetValue(BatteryLifeProperty, out object? raw)
                && raw is byte propLevel)
            {
                Log.Info("Classic '{Name}': battery = {Level}% (Windows device property)",
                    device.Name, propLevel);
                UpsertDevice(addressKey, device.Name, propLevel);
                return;
            }
            else
            {
                Log.Debug("Classic '{Name}': Windows battery property not available.",
                    device.Name);
            }

            // Strategy 2: GATT via BLE connection on the same Bluetooth address.
            // Many earbuds / headsets support dual-mode (Classic audio + BLE control).
            int? gattLevel = await TryReadGattBatteryByAddressAsync(
                device.BluetoothAddress, cancellationToken);

            if (gattLevel.HasValue)
            {
                Log.Info("Classic '{Name}': battery = {Level}% (GATT via BLE address)",
                    device.Name, gattLevel.Value);
                UpsertDevice(addressKey, device.Name, gattLevel.Value);
                return;
            }

            Log.Debug("Classic '{Name}': battery level not available via any strategy.",
                device.Name);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Error querying Classic device {Id}.", deviceInfo.Id);
        }
        finally
        {
            device?.Dispose();
        }
    }

    // -------------------------------------------------------------------------
    // Strategy 3: Pure BLE devices advertising the GATT Battery Service
    // -------------------------------------------------------------------------

    private async Task DiscoverLeGattDevicesAsync(CancellationToken cancellationToken)
    {
        string selector = GattDeviceService.GetDeviceSelectorFromUuid(BatteryServiceUuid);
        DeviceInformationCollection leDevices;

        try
        {
            leDevices = await DeviceInformation.FindAllAsync(selector)
                .AsTask(cancellationToken);

            Log.Debug("Found {Count} paired BLE GATT device(s).", leDevices.Count);
            foreach (var device in leDevices)
            {
                Log.Debug("Paired BLE GATT device: {Name} ({Id})", device.Name, device.Id);
            }
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to enumerate BLE GATT devices.");
            return;
        }

        Log.Info("BLE GATT: {Count} device(s) with Battery Service found.", leDevices.Count);

        await Task.WhenAll(leDevices.Select(
            d => QueryLeGattDeviceAsync(d, cancellationToken)));
    }

    private async Task QueryLeGattDeviceAsync(
        DeviceInformation deviceInfo, CancellationToken cancellationToken)
    {
        BluetoothLEDevice? bleDevice = null;
        GattDeviceService? batteryService = null;

        try
        {
            bleDevice = await BluetoothLEDevice.FromIdAsync(deviceInfo.Id)
                .AsTask(cancellationToken);

            if (bleDevice is null) return;

            string addressKey = bleDevice.BluetoothAddress.ToString();

            // Skip if already populated by the Classic path to avoid overwriting
            // a good reading with a potentially stale one.
            if (_devices.TryGetValue(addressKey, out BluetoothDeviceInfo? existing)
                && existing.BatteryPercent.HasValue)
            {
                Log.Debug("BLE GATT '{Name}': already tracked via Classic path. Skipping.",
                    bleDevice.Name);
                return;
            }

            GattDeviceServicesResult servicesResult =
                await bleDevice.GetGattServicesForUuidAsync(
                    BatteryServiceUuid, BluetoothCacheMode.Uncached)
                    .AsTask(cancellationToken);

            if (servicesResult.Status != GattCommunicationStatus.Success
                || servicesResult.Services.Count == 0)
            {
                Log.Debug("BLE '{Name}': Battery Service not accessible (status: {Status}).",
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
                return;

            GattReadResult readResult = await charsResult.Characteristics[0]
                .ReadValueAsync(BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken);

            if (readResult.Status != GattCommunicationStatus.Success) return;

            byte level = DataReader.FromBuffer(readResult.Value).ReadByte();
            level = Math.Min(level, (byte)100);

            Log.Info("BLE GATT '{Name}': battery = {Level}%", bleDevice.Name, level);
            UpsertDevice(addressKey, bleDevice.Name, level);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Error querying BLE device {Name} ({Id}).",
                deviceInfo.Name, deviceInfo.Id);
        }
        finally
        {
            batteryService?.Dispose();
            bleDevice?.Dispose();
        }
    }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens a BLE connection to <paramref name="bluetoothAddress"/> and reads
    /// the GATT Battery Level characteristic. Returns null if the device does not
    /// support BLE, does not have the Battery Service, or the read fails.
    /// </summary>
    private static async Task<int?> TryReadGattBatteryByAddressAsync(
        ulong bluetoothAddress, CancellationToken cancellationToken)
    {
        BluetoothLEDevice? leDevice = null;
        GattDeviceService? batteryService = null;

        try
        {
            leDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress)
                .AsTask(cancellationToken);

            if (leDevice is null) return null;

            var servicesResult = await leDevice
                .GetGattServicesForUuidAsync(BatteryServiceUuid, BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken);

            if (servicesResult.Status != GattCommunicationStatus.Success
                || servicesResult.Services.Count == 0)
                return null;

            batteryService = servicesResult.Services[0];

            var charsResult = await batteryService
                .GetCharacteristicsForUuidAsync(
                    BatteryLevelCharacteristicUuid, BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken);

            if (charsResult.Status != GattCommunicationStatus.Success
                || charsResult.Characteristics.Count == 0)
                return null;

            var readResult = await charsResult.Characteristics[0]
                .ReadValueAsync(BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken);

            if (readResult.Status != GattCommunicationStatus.Success) return null;

            byte level = DataReader.FromBuffer(readResult.Value).ReadByte();
            return Math.Min(level, (byte)100);
        }
        catch
        {
            // Not BLE-capable, not reachable, or service unavailable — that is expected.
            return null;
        }
        finally
        {
            batteryService?.Dispose();
            leDevice?.Dispose();
        }
    }

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

    private IReadOnlyList<BluetoothDeviceInfo> GetSnapshot() =>
        _devices.Values.ToList().AsReadOnly();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _devices.Clear();
    }
}

