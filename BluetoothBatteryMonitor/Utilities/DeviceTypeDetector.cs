using BluetoothBatteryMonitor.Models;

namespace BluetoothBatteryMonitor.Utilities;

/// <summary>
/// Detects the type of Bluetooth device based on its name or other characteristics.
/// </summary>
public static class DeviceTypeDetector
{
    private static readonly Dictionary<string, DeviceType> DeviceTypePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        // Earbuds
        { "airpods", DeviceType.Earbuds },
        { "earbud", DeviceType.Earbuds },
        { "buds", DeviceType.Earbuds },
        { "pods", DeviceType.Earbuds },
        { "ear", DeviceType.Earbuds },
        { "galaxy buds", DeviceType.Earbuds },
        { "pixel buds", DeviceType.Earbuds },
        { "powerbeats", DeviceType.Earbuds },
        { "jaybird", DeviceType.Earbuds },
        { "beats fit", DeviceType.Earbuds },
        { "jabra elite", DeviceType.Earbuds },
        { "sennheiser momentum true wireless", DeviceType.Earbuds },
        { "sony wf", DeviceType.Earbuds },
        { "wf-", DeviceType.Earbuds }, // Sony WF series

        // Speakers (check before headphones to prioritize specific patterns)
        { "speaker", DeviceType.Speaker },
        { "soundlink", DeviceType.Speaker },
        { "jbl", DeviceType.Speaker },
        { "flip", DeviceType.Speaker },
        { "charge", DeviceType.Speaker },
        { "megaboom", DeviceType.Speaker },
        { "boom", DeviceType.Speaker },
        { "echo", DeviceType.Speaker },
        { "homepod", DeviceType.Speaker },
        { "sonos", DeviceType.Speaker },

        // Headphones
        { "headphone", DeviceType.Headphones },
        { "beats", DeviceType.Headphones },
        { "sony wh", DeviceType.Headphones },
        { "wh-", DeviceType.Headphones }, // Sony WH series
        { "bose quietcomfort", DeviceType.Headphones },
        { "quietcomfort", DeviceType.Headphones },
        { "sennheiser", DeviceType.Headphones },
        { "audio-technica", DeviceType.Headphones },
        { "momentum", DeviceType.Headphones },
        { "px7", DeviceType.Headphones },
        { "px5", DeviceType.Headphones },
        { "h9", DeviceType.Headphones },
        { "h8", DeviceType.Headphones },
        { "h4", DeviceType.Headphones },
        { "studio", DeviceType.Headphones },
        { "solo", DeviceType.Headphones },

        // Headsets
        { "headset", DeviceType.Headset },
        { "gaming", DeviceType.Headset },
        { "arctis", DeviceType.Headset },
        { "hyperx", DeviceType.Headset },
        { "razer", DeviceType.Headset },
        { "turtle beach", DeviceType.Headset },
        { "steelseries", DeviceType.Headset },
        { "logitech g", DeviceType.Headset },
        { "corsair", DeviceType.Headset },

        // Mouse
        { "mouse", DeviceType.Mouse },
        { "mx master", DeviceType.Mouse },
        { "mx anywhere", DeviceType.Mouse },
        { "trackball", DeviceType.Mouse },

        // Keyboard
        { "keyboard", DeviceType.Keyboard },
        { "mx keys", DeviceType.Keyboard }
    };

    /// <summary>
    /// Detects the device type based on the device name.
    /// </summary>
    /// <param name="deviceName">The name of the Bluetooth device.</param>
    /// <returns>The detected device type, or DeviceType.Unknown if no match is found.</returns>
    public static DeviceType DetectDeviceType(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return DeviceType.Unknown;

        // Check each pattern to see if the device name contains it
        foreach (var pattern in DeviceTypePatterns)
        {
            if (deviceName.Contains(pattern.Key, StringComparison.OrdinalIgnoreCase))
            {
                return pattern.Value;
            }
        }

        return DeviceType.Unknown;
    }
}
