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

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Infrastructure", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Debug(outputTemplate: "[{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                shared: false,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();

        Log.Information("Logging initialised. Log directory: {LogDirectory}", logDirectory);
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
