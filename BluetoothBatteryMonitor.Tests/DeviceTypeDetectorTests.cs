using BluetoothBatteryMonitor.Models;
using BluetoothBatteryMonitor.Utilities;

namespace BluetoothBatteryMonitor.Tests;

public class DeviceTypeDetectorTests
{
    [Theory]
    [InlineData("AirPods Pro", DeviceType.Earbuds)]
    [InlineData("Galaxy Buds+", DeviceType.Earbuds)]
    [InlineData("Pixel Buds Pro", DeviceType.Earbuds)]
    [InlineData("Powerbeats Pro", DeviceType.Earbuds)]
    [InlineData("Sony WF-1000XM4", DeviceType.Earbuds)]
    [InlineData("Jabra Elite 75t", DeviceType.Earbuds)]
    public void DetectDeviceType_EarbudsDevices_ReturnsEarbuds(string deviceName, DeviceType expectedType)
    {
        var result = DeviceTypeDetector.DetectDeviceType(deviceName);
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData("Sony WH-1000XM5", DeviceType.Headphones)]
    [InlineData("Bose QuietComfort 45", DeviceType.Headphones)]
    [InlineData("Beats Studio Pro", DeviceType.Headphones)]
    [InlineData("Sennheiser Momentum 4", DeviceType.Headphones)]
    [InlineData("Audio-Technica ATH-M50x", DeviceType.Headphones)]
    public void DetectDeviceType_HeadphonesDevices_ReturnsHeadphones(string deviceName, DeviceType expectedType)
    {
        var result = DeviceTypeDetector.DetectDeviceType(deviceName);
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData("JBL Flip 6", DeviceType.Speaker)]
    [InlineData("Bose SoundLink", DeviceType.Speaker)]
    [InlineData("UE Megaboom 3", DeviceType.Speaker)]
    [InlineData("Amazon Echo", DeviceType.Speaker)]
    [InlineData("HomePod mini", DeviceType.Speaker)]
    [InlineData("Sonos Roam", DeviceType.Speaker)]
    public void DetectDeviceType_SpeakerDevices_ReturnsSpeaker(string deviceName, DeviceType expectedType)
    {
        var result = DeviceTypeDetector.DetectDeviceType(deviceName);
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData("SteelSeries Arctis 7", DeviceType.Headset)]
    [InlineData("HyperX Cloud II", DeviceType.Headset)]
    [InlineData("Razer BlackShark V2", DeviceType.Headset)]
    [InlineData("Logitech G Pro X", DeviceType.Headset)]
    [InlineData("Corsair HS70", DeviceType.Headset)]
    public void DetectDeviceType_HeadsetDevices_ReturnsHeadset(string deviceName, DeviceType expectedType)
    {
        var result = DeviceTypeDetector.DetectDeviceType(deviceName);
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData("MX Master 3", DeviceType.Mouse)]
    [InlineData("Logitech MX Anywhere 2S", DeviceType.Mouse)]
    [InlineData("Bluetooth Mouse", DeviceType.Mouse)]
    public void DetectDeviceType_MouseDevices_ReturnsMouse(string deviceName, DeviceType expectedType)
    {
        var result = DeviceTypeDetector.DetectDeviceType(deviceName);
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData("MX Keys", DeviceType.Keyboard)]
    [InlineData("Bluetooth Keyboard", DeviceType.Keyboard)]
    public void DetectDeviceType_KeyboardDevices_ReturnsKeyboard(string deviceName, DeviceType expectedType)
    {
        var result = DeviceTypeDetector.DetectDeviceType(deviceName);
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData("Unknown Device", DeviceType.Unknown)]
    [InlineData("", DeviceType.Unknown)]
    [InlineData(null, DeviceType.Unknown)]
    [InlineData("   ", DeviceType.Unknown)]
    public void DetectDeviceType_UnknownDevices_ReturnsUnknown(string? deviceName, DeviceType expectedType)
    {
        var result = DeviceTypeDetector.DetectDeviceType(deviceName!);
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData("AIRPODS PRO", DeviceType.Earbuds)]
    [InlineData("airpods pro", DeviceType.Earbuds)]
    [InlineData("AiRpOdS pRo", DeviceType.Earbuds)]
    public void DetectDeviceType_CaseInsensitive_ReturnsCorrectType(string deviceName, DeviceType expectedType)
    {
        var result = DeviceTypeDetector.DetectDeviceType(deviceName);
        Assert.Equal(expectedType, result);
    }
}
