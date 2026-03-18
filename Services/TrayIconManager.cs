using System.Drawing;
using System.IO;
using System.Windows.Forms;
using BluetoothBatteryMonitor.Models;
using NLog;

namespace BluetoothBatteryMonitor.Services;

/// <summary>
/// Manages the Windows system tray icon, tooltip, and context menu.
/// Uses System.Windows.Forms.NotifyIcon hosted in a WPF application.
/// </summary>
public class TrayIconManager : IDisposable
{
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly int _lowBatteryThreshold;

    private bool _disposed;

    public event EventHandler? SettingsRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler? ExitRequested;

    public TrayIconManager(int lowBatteryThreshold = 20)
    {
        _lowBatteryThreshold = lowBatteryThreshold;

        _contextMenu = BuildContextMenu();

        _notifyIcon = new NotifyIcon
        {
            Text = "Bluetooth Battery Monitor",
            Visible = true,
            ContextMenuStrip = _contextMenu,
            Icon = LoadIcon()
        };

        _notifyIcon.MouseClick += OnTrayIconClick;

        Log.Debug("TrayIconManager initialized.");
    }

    private Icon LoadIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        // Fallback: use a built-in system icon
        Log.Warn("icon.ico not found at {Path}. Using system fallback.", iconPath);
        return SystemIcons.Application;
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        var refreshItem = new ToolStripMenuItem("Refresh Now");
        refreshItem.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(refreshItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OnTrayIconClick(object? sender, MouseEventArgs e)
    {
        // Left single-click — show balloon with current battery info
        if (e.Button == MouseButtons.Left)
        {
            _notifyIcon.ShowBalloonTip(
                timeout: 4000,
                tipTitle: "Bluetooth Battery Levels",
                tipText: _notifyIcon.Text,
                tipIcon: ToolTipIcon.Info);
        }
    }

    /// <summary>
    /// Updates the tray tooltip and icon colour based on the latest device list.
    /// Must be called from the UI / WinForms message loop thread, or use Invoke.
    /// </summary>
    public void UpdateDevices(IReadOnlyList<BluetoothDeviceInfo> devices)
    {
        if (devices.Count == 0)
        {
            SetTooltip("No Bluetooth audio devices found.");
            SetIcon(IconState.Normal);
            return;
        }

        var lines = devices
            .OrderBy(d => d.Name)
            .Select(d => d.ToDisplayString())
            .ToList();

        // NotifyIcon.Text has a 127-character limit in older Windows versions; truncate safely.
        string fullText = string.Join(" | ", lines);
        SetTooltip(fullText);

        bool anyLow = devices.Any(d => d.BatteryPercent.HasValue && d.BatteryPercent.Value <= _lowBatteryThreshold);
        SetIcon(anyLow ? IconState.LowBattery : IconState.Normal);

        Log.Debug("Tray tooltip updated: {Text}", fullText);
    }

    /// <summary>Shows a balloon warning notification for a low-battery device.</summary>
    public void ShowLowBatteryNotification(BluetoothDeviceInfo device)
    {
        _notifyIcon.ShowBalloonTip(
            timeout: 6000,
            tipTitle: "Low Bluetooth Battery",
            tipText: $"{device.Name} is at {device.BatteryPercent}%.",
            tipIcon: ToolTipIcon.Warning);

        Log.Info("Low battery notification shown for {Device} ({Battery}%)",
            device.Name, device.BatteryPercent);
    }

    private void SetTooltip(string text)
    {
        // WinForms NotifyIcon silently truncates at 127 characters; clamp gracefully.
        const int MaxTooltipLength = 127;
        _notifyIcon.Text = text.Length > MaxTooltipLength
            ? string.Concat(text.AsSpan(0, MaxTooltipLength - 3), "...")
            : text;
    }

    private enum IconState { Normal, LowBattery }

    private void SetIcon(IconState state)
    {
        // Future: swap in a red icon when low battery.  For now one icon is used.
        // Icon swapping can be added when custom icon assets are available.
        _ = state;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _notifyIcon.Visible = false;
        _notifyIcon.MouseClick -= OnTrayIconClick;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();

        Log.Debug("TrayIconManager disposed.");
    }
}
