using BluetoothBatteryMonitor.Models;

namespace BluetoothBatteryMonitor.Tests;

public class BluetoothDeviceInfoTests
{
    [Fact]
    public void ToDisplayString_WithHeadphones_IncludesHeadphoneIcon()
    {
        var device = new BluetoothDeviceInfo
        {
            Name = "Test Headphones",
            BatteryPercent = 85,
            DeviceType = DeviceType.Headphones
        };

        var result = device.ToDisplayString();

        Assert.Contains("🎧", result);
        Assert.Contains("Test Headphones", result);
        Assert.Contains("85%", result);
    }

    [Fact]
    public void ToDisplayString_WithEarbuds_IncludesHeadphoneIcon()
    {
        var device = new BluetoothDeviceInfo
        {
            Name = "AirPods Pro",
            BatteryPercent = 75,
            DeviceType = DeviceType.Earbuds
        };

        var result = device.ToDisplayString();

        Assert.Contains("🎧", result);
        Assert.Contains("AirPods Pro", result);
        Assert.Contains("75%", result);
    }

    [Fact]
    public void ToDisplayString_WithSpeaker_IncludesSpeakerIcon()
    {
        var device = new BluetoothDeviceInfo
        {
            Name = "JBL Flip 6",
            BatteryPercent = 50,
            DeviceType = DeviceType.Speaker
        };

        var result = device.ToDisplayString();

        Assert.Contains("🔊", result);
        Assert.Contains("JBL Flip 6", result);
        Assert.Contains("50%", result);
    }

    [Fact]
    public void ToDisplayString_WithHeadset_IncludesHeadsetIcon()
    {
        var device = new BluetoothDeviceInfo
        {
            Name = "Gaming Headset",
            BatteryPercent = 30,
            DeviceType = DeviceType.Headset
        };

        var result = device.ToDisplayString();

        Assert.Contains("🎮", result);
        Assert.Contains("Gaming Headset", result);
        Assert.Contains("30%", result);
    }

    [Fact]
    public void ToDisplayString_WithMouse_IncludesMouseIcon()
    {
        var device = new BluetoothDeviceInfo
        {
            Name = "MX Master 3",
            BatteryPercent = 60,
            DeviceType = DeviceType.Mouse
        };

        var result = device.ToDisplayString();

        Assert.Contains("🖱️", result);
        Assert.Contains("MX Master 3", result);
        Assert.Contains("60%", result);
    }

    [Fact]
    public void ToDisplayString_WithKeyboard_IncludesKeyboardIcon()
    {
        var device = new BluetoothDeviceInfo
        {
            Name = "MX Keys",
            BatteryPercent = 90,
            DeviceType = DeviceType.Keyboard
        };

        var result = device.ToDisplayString();

        Assert.Contains("⌨️", result);
        Assert.Contains("MX Keys", result);
        Assert.Contains("90%", result);
    }

    [Fact]
    public void ToDisplayString_WithUnknownType_ShowsNoIcon()
    {
        var device = new BluetoothDeviceInfo
        {
            Name = "Unknown Device",
            BatteryPercent = 100,
            DeviceType = DeviceType.Unknown
        };

        var result = device.ToDisplayString();

        Assert.Equal("Unknown Device: 100%", result);
    }

    [Fact]
    public void ToDisplayString_WithNullBattery_ShowsNA()
    {
        var device = new BluetoothDeviceInfo
        {
            Name = "Test Device",
            BatteryPercent = null,
            DeviceType = DeviceType.Headphones
        };

        var result = device.ToDisplayString();

        Assert.Contains("🎧", result);
        Assert.Contains("Test Device", result);
        Assert.Contains("N/A", result);
    }

    [Fact]
    public void ToDisplayString_WithZeroBattery_ShowsZeroPercent()
    {
        var device = new BluetoothDeviceInfo
        {
            Name = "Dead Device",
            BatteryPercent = 0,
            DeviceType = DeviceType.Earbuds
        };

        var result = device.ToDisplayString();

        Assert.Contains("🎧", result);
        Assert.Contains("Dead Device", result);
        Assert.Contains("0%", result);
    }

    [Fact]
    public void DeviceType_DefaultsToUnknown()
    {
        var device = new BluetoothDeviceInfo
        {
            Name = "Test"
        };

        Assert.Equal(DeviceType.Unknown, device.DeviceType);
    }
}
