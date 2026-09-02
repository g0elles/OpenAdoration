using OpenAdoration.WPF.ViewModels;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Stage;

/// <summary>
/// F9: the F7 style editor gate (<c>IsStyleEditorLive</c>) must recognize Notes content the same
/// way it already does Song -- <see cref="StageViewModel.IsNotesContextKey"/> mirrors
/// <see cref="StageViewModelSongContextKeyTests"/> exactly, since Notes is a real library entity
/// like Song (not a bare reference like Bible). Regression coverage for a real bug caught via GUI
/// verification: the gate's boolean expression was never extended when Notes contextKeys were
/// added, so the F7 bar silently never appeared for Notes projections at all.
/// </summary>
public sealed class StageViewModelNotesContextKeyTests
{
    [Theory]
    [InlineData("notes:42")]
    [InlineData("service-notes:7")]
    public void IsNotesContextKey_NotesOrServiceNotesKey_ReturnsTrue(string contextKey)
    {
        Assert.True(StageViewModel.IsNotesContextKey(contextKey));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("song:42")]
    [InlineData("service-song:7")]
    [InlineData("service-bible:7")]
    [InlineData("bible:standalone")]
    [InlineData("notes:")]
    [InlineData("notes:abc")]
    [InlineData("note:42")] // near-miss prefix must not match
    public void IsNotesContextKey_NonNotesOrMalformedKey_ReturnsFalse(string? contextKey)
    {
        Assert.False(StageViewModel.IsNotesContextKey(contextKey));
    }
}
