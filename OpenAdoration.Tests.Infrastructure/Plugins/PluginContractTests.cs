using OpenAdoration.Plugins.Abstractions;
using OpenAdoration.Plugins.Sample;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Plugins;

/// <summary>
/// M13.1: the plugin contract is implementable from a separate assembly that references only
/// Abstractions, and a Bible-source plugin returns usable DTO data through the capability API.
/// </summary>
public sealed class PluginContractTests
{
    [Fact]
    public async Task EchoPlugin_FulfilsBibleSourceContract()
    {
        IBibleSourcePlugin plugin = new EchoBibleSourcePlugin();

        Assert.Equal("sample.echo", plugin.Id);
        Assert.Equal(new Version(1, 0, 0), plugin.Version);

        var versions = await plugin.GetAvailableVersionsAsync();
        var version = Assert.Single(versions);

        var fetched = 0;
        var data = await plugin.FetchAsync(version.Id, new Progress<int>(n => fetched = n));

        Assert.Equal("ECHO", data.Version.Abbreviation);
        var book = Assert.Single(data.Books);
        Assert.Equal(PluginTestament.Old, book.Testament);
        var verse = Assert.Single(data.Verses);
        Assert.Equal(("Genesis", 1, 1), (verse.Book, verse.Chapter, verse.Verse));
    }
}
