using System.IO;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace BluetoothBatteryMonitor.Utilities;

/// <summary>
/// Configures NLog programmatically for file-based logging.
/// Log files are written to %LOCALAPPDATA%\BluetoothBatteryMonitor\logs\.
/// </summary>
public static class LoggingSetup
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BluetoothBatteryMonitor", "logs");

    public static void Configure(string logLevelName)
    {
        LogLevel level = ParseLevel(logLevelName);

        var config = new LoggingConfiguration();

        var fileTarget = new FileTarget("logfile")
        {
            FileName = Path.Combine(LogDirectory, "app.log"),
            Layout = "${longdate} [${level:uppercase=true}] ${logger:shortName=true} — ${message}${onexception:inner= | Exception: ${exception:format=tostring}}",
            ArchiveAboveSize = 5 * 1024 * 1024, // 5 MB
            MaxArchiveFiles = 3,
            ArchiveNumbering = ArchiveNumberingMode.Rolling,
            ConcurrentWrites = false,
            KeepFileOpen = true,
            Encoding = System.Text.Encoding.UTF8
        };

        var consoleTarget = new ConsoleTarget("logconsole")
        {
            Layout = "${longdate} [${level:uppercase=true}] ${logger:shortName=true} — ${message}${onexception:inner= | Exception: ${exception:format=tostring}}"
        };

        config.AddRule(level, LogLevel.Fatal, fileTarget);
        config.AddRule(level, LogLevel.Fatal, consoleTarget);
        LogManager.Configuration = config;
    }

    private static LogLevel ParseLevel(string name) => name?.ToLowerInvariant() switch
    {
        "trace" => LogLevel.Trace,
        "debug" => LogLevel.Debug,
        "warn" or "warning" => LogLevel.Warn,
        "error" => LogLevel.Error,
        "off" => LogLevel.Off,
        _ => LogLevel.Info
    };
}
