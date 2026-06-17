using OpenAdoration.Infrastructure.Update;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Update;

public class GitHubUpdateServiceTests
{
    private static string Release(string tag, params string[] assetNames)
    {
        var assets = string.Join(",", assetNames.Select(n =>
            $$"""{"name":"{{n}}","browser_download_url":"https://example/{{n}}","size":1048576}"""));
        return $$"""{"tag_name":"{{tag}}","html_url":"https://example/release","assets":[{{assets}}]}""";
    }

    [Fact]
    public void NewerVersionWithMsi_ReturnsUpdate()
    {
        var info = GitHubUpdateService.ParseLatestRelease(
            Release("v1.2.0", "OpenAdoration-1.2.0-win-x64.msi"), new Version(1, 1, 0));

        Assert.NotNull(info);
        Assert.Equal(new Version(1, 2, 0), info!.Version);
        Assert.EndsWith(".msi", info.MsiUrl);
        Assert.Equal(1048576, info.MsiSizeBytes);
    }

    [Fact]
    public void SameOrOlderVersion_ReturnsNull()
    {
        Assert.Null(GitHubUpdateService.ParseLatestRelease(Release("v1.1.0", "x.msi"), new Version(1, 1, 0)));
        Assert.Null(GitHubUpdateService.ParseLatestRelease(Release("1.0.0", "x.msi"), new Version(1, 1, 0)));
    }

    [Fact]
    public void NewerVersionButNoMsiAsset_ReturnsNull()
    {
        Assert.Null(GitHubUpdateService.ParseLatestRelease(Release("v2.0.0", "notes.txt", "src.zip"), new Version(1, 1, 0)));
    }

    [Fact]
    public void MissingOrUnparsableTag_ReturnsNull()
    {
        Assert.Null(GitHubUpdateService.ParseLatestRelease("""{"html_url":"x","assets":[]}""", new Version(1, 0, 0)));
        Assert.Null(GitHubUpdateService.ParseLatestRelease(Release("nightly", "x.msi"), new Version(1, 0, 0)));
    }
}
