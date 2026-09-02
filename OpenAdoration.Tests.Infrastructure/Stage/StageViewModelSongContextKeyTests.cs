using OpenAdoration.WPF.ViewModels;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Stage;

/// <summary>
/// F7: the Stage View quick style fix (font size / colour override) is song-only, gated by
/// <see cref="StageViewModel.IsSongContextKey"/> — a thin public wrapper around the internal
/// <c>ProjectionContextKeys.TryGetSongId</c> parser (WPF.Services is internal, so the test
/// project — a separate assembly — can't call it directly; this static passthrough, mirroring
/// <see cref="StageViewModel.ComputeMirrorScale"/>, is the unit-testable seam).
/// </summary>
public sealed class StageViewModelSongContextKeyTests
{
    [Theory]
    [InlineData("song:42")]
    [InlineData("service-song:7")]
    public void IsSongContextKey_SongOrServiceSongKey_ReturnsTrue(string contextKey)
    {
        Assert.True(StageViewModel.IsSongContextKey(contextKey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bible:John 3:16")]
    [InlineData("media:42")]
    [InlineData("song:")]
    [InlineData("song:abc")]
    [InlineData("songs:42")] // near-miss prefix must not match
    public void IsSongContextKey_NonSongOrMalformedKey_ReturnsFalse(string? contextKey)
    {
        Assert.False(StageViewModel.IsSongContextKey(contextKey));
    }
}
