using System.Windows;
using System.Windows.Controls;
using BluetoothBatteryMonitor.Models;
using BluetoothBatteryMonitor.Services;

// Disambiguate from System.Windows.Forms.ComboBox
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace BluetoothBatteryMonitor.UI;

public partial class SettingsWindow : Window
{
    private readonly ConfigManager _configManager;

    public SettingsWindow(ConfigManager configManager)
    {
        _configManager = configManager;
        InitializeComponent();
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        AppConfig cfg = _configManager.Config;

        // Clamp stored value to slider range just in case
        RefreshSlider.Value = Math.Clamp(cfg.RefreshIntervalSeconds, 60, 600);
        UpdateRefreshLabel(RefreshSlider.Value);

        ThresholdSlider.Value = Math.Clamp(cfg.LowBatteryWarningThreshold, 5, 50);
        UpdateThresholdLabel(ThresholdSlider.Value);

        SelectComboItem(LogLevelCombo, cfg.LogLevel ?? "Info");

        AutoStartCheckBox.IsChecked = cfg.AutoStart;
    }

    private void RefreshSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => UpdateRefreshLabel(e.NewValue);

    private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => UpdateThresholdLabel(e.NewValue);

    private void UpdateRefreshLabel(double seconds)
    {
        if (RefreshValueLabel is null) return;
        int totalSeconds = (int)seconds;
        RefreshValueLabel.Text = totalSeconds < 120
            ? $"{totalSeconds} sec"
            : $"{totalSeconds / 60} min";
    }

    private void UpdateThresholdLabel(double value)
    {
        if (ThresholdValueLabel is null) return;
        ThresholdValueLabel.Text = $"{(int)value}%";
    }

    private static void SelectComboItem(ComboBox combo, string value)
    {
        foreach (ComboBoxItem item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value,
                StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        // Default to Info
        combo.SelectedIndex = 2;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        AppConfig updated = new()
        {
            RefreshIntervalSeconds = (int)RefreshSlider.Value,
            LowBatteryWarningThreshold = (int)ThresholdSlider.Value,
            LogLevel = (LogLevelCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Info",
            AutoStart = AutoStartCheckBox.IsChecked ?? false
        };

        _configManager.Update(updated);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
