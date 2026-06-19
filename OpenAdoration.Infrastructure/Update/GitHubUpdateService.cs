using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;

namespace OpenAdoration.Infrastructure.Update;

/// <summary>
/// Checks the project's GitHub releases for a newer MSI and hands off to msiexec. The only
/// outbound call OA makes; opt-in, update-only, no telemetry. Any failure (offline, rate-limit,
/// malformed) is swallowed so a missing network never disrupts the operator.
/// </summary>
public sealed class GitHubUpdateService : IUpdateService
{
    // ponytail: the repo never changes; a const beats a config knob nobody turns.
    private const string LatestReleaseUrl = "https://api.github.com/repos/g0elles/OpenAdoration/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private readonly ILogger<GitHubUpdateService> _logger;

    public GitHubUpdateService(ILogger<GitHubUpdateService> logger) => _logger = logger;

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // GitHub's API rejects requests with no User-Agent.
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OpenAdoration", "1.0"));
        return http;
    }

    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
            var json = await Http.GetStringAsync(LatestReleaseUrl, ct);
            return ParseLatestRelease(json, current);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Update check skipped (offline or unavailable).");
            return null;
        }
    }

    /// <summary>
    /// Pure parse + compare, split out so the version/asset logic is testable without a network call.
    /// Returns the update only when the release tag is a strictly higher version and carries an .msi asset.
    /// </summary>
    public static UpdateInfo? ParseLatestRelease(string json, Version current)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("tag_name", out var tag) || tag.GetString() is not { } tagText)
            return null;
        if (!TryParseTag(tagText, out var version) || version <= current)
            return null;

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name is null || !name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) continue;

            var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
            if (url is null) continue;

            var size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var bytes) ? bytes : 0;
            var notes = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? url : url;
            return new UpdateInfo(version, notes, url, size);
        }

        return null;
    }

    private static bool TryParseTag(string tag, out Version version) =>
        Version.TryParse(tag.TrimStart('v', 'V'), out version!);

    public async Task DownloadAndApplyAsync(UpdateInfo info, CancellationToken ct = default)
    {
        var msiPath = Path.Combine(Path.GetTempPath(), $"OpenAdoration-{info.Version}.msi");
        await using (var src = await Http.GetStreamAsync(info.MsiUrl, ct))
        await using (var dst = File.Create(msiPath))
            await src.CopyToAsync(dst, ct);

        _logger.LogInformation("Launching installer for v{Version}", info.Version);
        Process.Start(new ProcessStartInfo("msiexec", $"/i \"{msiPath}\"") { UseShellExecute = true });
    }
}
