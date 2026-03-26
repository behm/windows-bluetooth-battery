using System.Windows;

namespace BluetoothBatteryMonitor;

/// <summary>
/// Hidden anchor window — never shown to the user.
/// Exists purely to satisfy WPF's requirement for a main window
/// while the app runs as a system-tray-only application.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
