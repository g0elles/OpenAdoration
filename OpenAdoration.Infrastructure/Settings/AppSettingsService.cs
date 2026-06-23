using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;

namespace OpenAdoration.Infrastructure.Settings;

/// <summary>
/// JSON-file-backed app settings. Loads once at construction (defaults on missing
/// or corrupt file) and rewrites the file atomically on each save.
/// </summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILogger<AppSettingsService> _logger;

    // Tracks the most recent write so a fire-and-forget save can be awaited at shutdown (H4).
    private Task _lastWrite = Task.CompletedTask;

    public AppSettings Current { get; private set; }

    public AppSettingsService(string filePath, ILogger<AppSettingsService> logger)
    {
        _filePath = filePath;
        _logger   = logger;
        Current   = Load();
    }

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _lastWrite = WriteAsync(settings, ct);
    }

    public Task FlushAsync() => _lastWrite;

    private async Task WriteAsync(AppSettings settings, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        await File.WriteAllTextAsync(_filePath, json, ct);
        Current = settings;
        _logger.LogInformation("App settings saved to {Path}", _filePath);
    }

    private AppSettings Load()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read settings from {Path} — falling back to defaults", _filePath);
            return new AppSettings();
        }
    }
}
