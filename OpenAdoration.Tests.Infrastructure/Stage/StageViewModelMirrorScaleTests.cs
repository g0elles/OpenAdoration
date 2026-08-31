using OpenAdoration.WPF.ViewModels;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Stage;

/// <summary>
/// Follow-up to M15's lower-third mirror (fa9a531): the stage preview renders the lower third
/// inside a fixed 1920×1080 design canvas that a Viewbox scales uniformly to the small panel.
/// <see cref="StageViewModel.ComputeMirrorScale"/> pre-scales the settings-tuned font
/// size / scroll speed by (design ÷ real projector resolution) so the value baked into that
/// canvas — once the panel's own Viewbox shrinks it further — reproduces the same on-screen
/// proportion the real projector shows, at any real resolution (not just 1920×1080).
/// </summary>
public sealed class StageViewModelMirrorScaleTests
{
    [Fact]
    public void ComputeMirrorScale_RealResolutionMatchesDesign_ReturnsOne()
    {
        var (scaleX, scaleY) = StageViewModel.ComputeMirrorScale(1920, 1080);

        Assert.Equal(1.0, scaleX, 3);
        Assert.Equal(1.0, scaleY, 3);
    }

    [Fact]
    public void ComputeMirrorScale_SmallerRealScreen_ScalesUp()
    {
        // A 1280x720 projector: a fixed-point-size font/speed occupies a larger fraction of the
        // real screen than it would at 1920x1080, so the canvas-space value must grow to match.
        var (scaleX, scaleY) = StageViewModel.ComputeMirrorScale(1280, 720);

        Assert.Equal(1.5, scaleX, 3);
        Assert.Equal(1.5, scaleY, 3);
    }

    [Fact]
    public void ComputeMirrorScale_LargerRealScreen_ScalesDown()
    {
        // A 4K projector: the same absolute font size is relatively smaller on the real screen.
        var (scaleX, scaleY) = StageViewModel.ComputeMirrorScale(3840, 2160);

        Assert.Equal(0.5, scaleX, 3);
        Assert.Equal(0.5, scaleY, 3);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(-100, 1080)]
    public void ComputeMirrorScale_InvalidRealResolution_FallsBackToOne(int width, int height)
    {
        var (scaleX, scaleY) = StageViewModel.ComputeMirrorScale(width, height);

        Assert.Equal(1.0, scaleX, 3);
        Assert.Equal(1.0, scaleY, 3);
    }
}
