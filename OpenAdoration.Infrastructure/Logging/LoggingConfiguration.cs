using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace OpenAdoration.Infrastructure.Logging;

public static class LoggingConfiguration
{
    /// <summary>
    /// Configures Serilog with a rolling file sink and a debug sink.
    /// Call this once at application startup, before building the host.
    /// </summary>
    /// <param name="logDirectory">Directory where log files will be written.</param>
    public static void Configure(string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

        Directory.CreateDirectory(logDirectory);

        var logPath = Path.Combine(logDirectory, "openadoration-.log");

        const LogEventLevel defaultLevel = LogEventLevel.Information;

        // Short session ID for correlating all log entries within one app run (L3)
        var sessionId  = Guid.NewGuid().ToString("N")[..8];
        var appVersion = System.Reflection.Assembly.GetEntryAssembly()
                             ?.GetName().Version?.ToString(3) ?? "unknown";
        var osVersion  = Environment.OSVersion.VersionString;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(defaultLevel)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Infrastructure", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("SessionId",  sessionId)   // (L3) per-run correlation ID
            .Enrich.WithProperty("AppVersion", appVersion)  // (L3) version for support
            .Enrich.WithProperty("OS",         osVersion)   // (L3) environment context
            .WriteTo.Debug(outputTemplate: "[{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10_485_760,    // 10 MB per file (L6)
                rollOnFileSizeLimit: true,          // roll when size is hit, not only on day change (L6)
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SessionId}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                shared: true,                       // allows live tail with Get-Content -Wait (L9)
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();

        Log.Information(
            "Logging initialised — Session={SessionId}, Version={AppVersion}, OS={OS}, LogDirectory={LogDirectory}",
            sessionId, appVersion, osVersion, logDirectory);
    }

    /// <summary>
    /// Wires the pre-configured Serilog logger into Microsoft.Extensions.Logging.
    /// Call Configure() before calling this.
    /// </summary>
    public static ILoggingBuilder UseOpenAdorationSerilog(this ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.AddSerilog(Log.Logger, dispose: false);
        return builder;
    }

    /// <summary>
    /// Flushes buffered log entries and releases the file handle.
    /// Call this in the application's shutdown handler.
    /// </summary>
    public static void CloseAndFlush() => Log.CloseAndFlush();
}
