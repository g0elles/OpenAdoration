using OpenAdoration.WPF.ViewModels;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Stage;

/// <summary>
/// F7 rebuild: a live style edit must clone the effective theme before mutating it whenever the
/// scope level being edited (Song or ScheduleItem) has no explicit theme of its own yet, or its
/// resolved effective theme happens to be the shared app-wide default — otherwise the edit would
/// silently repaint every other song/occurrence that also has no explicit theme.
/// <see cref="StageViewModel.ShouldCloneBeforeEdit"/> is the pure predicate behind that guard.
/// </summary>
public sealed class StageViewModelShouldCloneBeforeEditTests
{
    [Fact]
    public void NoOwnThemeId_ReturnsTrue()
    {
        Assert.True(StageViewModel.ShouldCloneBeforeEdit(scopeOwnThemeId: null, effectiveThemeIsDefault: false));
    }

    [Fact]
    public void OwnThemeIdSetButResolvesToTheSharedDefault_ReturnsTrue()
    {
        Assert.True(StageViewModel.ShouldCloneBeforeEdit(scopeOwnThemeId: 3, effectiveThemeIsDefault: true));
    }

    [Fact]
    public void OwnThemeIdSetToADedicatedNonDefaultTheme_ReturnsFalse()
    {
        Assert.False(StageViewModel.ShouldCloneBeforeEdit(scopeOwnThemeId: 3, effectiveThemeIsDefault: false));
    }
}
