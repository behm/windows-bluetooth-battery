using System.Drawing;
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
    private int _lowBatteryThreshold;
    private int _reallyLowBatteryThreshold;

    private bool _disposed;

    public event EventHandler? SettingsRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler? ExitRequested;

    public TrayIconManager(int lowBatteryThreshold = 20, int reallyLowBatteryThreshold = 5)
    {
        _lowBatteryThreshold = lowBatteryThreshold;
        _reallyLowBatteryThreshold = reallyLowBatteryThreshold;
        
        _contextMenu = BuildContextMenu();

        _notifyIcon = new NotifyIcon
        {
            Text = "Bluetooth Battery Monitor",
            Visible = true,
            ContextMenuStrip = _contextMenu,
            Icon = CreateBatteryIcon(Color.Gray)
        };

        _notifyIcon.MouseClick += OnTrayIconClick;

        Log.Debug("TrayIconManager initialized.");
    }

    private static Icon CreateBatteryIcon(Color color)
    {
        const int size = 16;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Battery body
        using var fill = new SolidBrush(color);
        g.FillRectangle(fill, 1, 3, 12, 10);

        // Battery outline
        using var outline = new Pen(Color.White, 1);
        g.DrawRectangle(outline, 1, 3, 12, 10);

        // Battery terminal nub
        g.FillRectangle(Brushes.White, 13, 5, 2, 6);

        return Icon.FromHandle(bmp.GetHicon());
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
    /// Updates the low-battery warning thresholds used when colouring the tray icon.
    /// Call this after saving new settings so that icon colours reflect the updated values immediately.
    /// Both this method and <see cref="UpdateDevices"/> must be called from the WPF dispatcher thread,
    /// which serialises access to these fields.
    /// </summary>
    public void UpdateThresholds(int lowBatteryThreshold, int reallyLowBatteryThreshold)
    {
        _lowBatteryThreshold = lowBatteryThreshold;
        _reallyLowBatteryThreshold = reallyLowBatteryThreshold;
        Log.Debug("TrayIconManager thresholds updated: Low={Low}, ReallyLow={ReallyLow}",
            lowBatteryThreshold, reallyLowBatteryThreshold);
    }

    /// <summary>
    /// Updates the tray tooltip and icon colour based on the latest device list.
    /// Must be called from the UI / WinForms message loop thread, or use Invoke.
    /// </summary>
    public void UpdateDevices(IReadOnlyList<BluetoothDeviceInfo> devices)
    {
        if (devices.Count == 0)
        {
            SetTooltip("No Bluetooth devices found.");
            SetIcon(IconState.NoDevices);
            return;
        }

        var lines = devices
            .OrderBy(d => d.Name)
            .Select(d => d.ToDisplayString())
            .ToList();

        // NotifyIcon.Text has a 127-character limit in older Windows versions; truncate safely.
        string fullText = string.Join("\n", lines);
        SetTooltip(fullText);

        bool anyLow = devices.Any(d => d.BatteryPercent.HasValue && d.BatteryPercent.Value <= _lowBatteryThreshold);
        bool anyReallyLow = devices.Any(d => d.BatteryPercent.HasValue && d.BatteryPercent.Value <= _reallyLowBatteryThreshold);

        SetIcon(anyLow 
                    ? anyReallyLow ? IconState.ReallyLowBattery : IconState.LowBattery
                    : IconState.Normal);

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

    private enum IconState { Normal, LowBattery, ReallyLowBattery, NoDevices }

    private IconState _currentIconState = IconState.NoDevices;

    private void SetIcon(IconState state)
    {
        if (state == _currentIconState && _notifyIcon.Icon is not null)
            return;

        _currentIconState = state;

        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = state switch
        {
            IconState.LowBattery => CreateBatteryIcon(Color.Gold),
            IconState.ReallyLowBattery => CreateBatteryIcon(Color.Red),
            IconState.NoDevices => CreateBatteryIcon(Color.Gray),
            _ => CreateBatteryIcon(Color.LimeGreen)
        };
        oldIcon?.Dispose();
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
