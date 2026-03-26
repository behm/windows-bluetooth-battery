using System.Windows;
using BluetoothBatteryMonitor.Utilities;

// Disambiguate from System.Windows.Forms.Application
using Application = System.Windows.Application;

namespace BluetoothBatteryMonitor;

public partial class App : Application
{
    private AppBootstrap? _bootstrap;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent multiple instances
        if (!SingleInstanceGuard.Acquire())
        {
            Shutdown();
            return;
        }

        // Keep app alive even with no open windows (tray app)
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _bootstrap = new AppBootstrap();
        _bootstrap.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _bootstrap?.Shutdown();
        SingleInstanceGuard.Release();
        base.OnExit(e);
    }
}

