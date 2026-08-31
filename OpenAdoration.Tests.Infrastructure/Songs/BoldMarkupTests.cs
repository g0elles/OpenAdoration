using OpenAdoration.Domain.Common;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Songs;

public sealed class BoldMarkupTests
{
    [Fact]
    public void Split_PlainText_ReturnsOneUnboldSegment()
    {
        var segments = BoldMarkup.Split("Jesus loves me");
        Assert.Single(segments);
        Assert.Equal(new BoldMarkup.Segment("Jesus loves me", false), segments[0]);
    }

    [Fact]
    public void Split_MidTextBold_ReturnsThreeSegments()
    {
        var segments = BoldMarkup.Split("Jesus **loves** me");
        Assert.Equal(
        [
            new BoldMarkup.Segment("Jesus ", false),
            new BoldMarkup.Segment("loves", true),
            new BoldMarkup.Segment(" me", false),
        ], segments);
    }

    [Fact]
    public void Split_WholeSlideBold_ReturnsOneBoldSegment()
    {
        var segments = BoldMarkup.Split("**Hallelujah**");
        Assert.Single(segments);
        Assert.Equal(new BoldMarkup.Segment("Hallelujah", true), segments[0]);
    }

    [Fact]
    public void ToggleSelection_EmptySelection_InsertsEmptyPairAndPlacesCaretBetween()
    {
        var (text, start, length) = BoldMarkup.ToggleSelection("Jesus  me", 6, 0);
        Assert.Equal("Jesus **** me", text);
        Assert.Equal(8, start);
        Assert.Equal(0, length);
    }

    [Fact]
    public void ToggleSelection_PlainSelection_Wraps()
    {
        var (text, start, length) = BoldMarkup.ToggleSelection("Jesus loves me", 6, 5);
        Assert.Equal("Jesus **loves** me", text);
        Assert.Equal(8, start);
        Assert.Equal(5, length);
    }

    [Fact]
    public void ToggleSelection_AlreadyBoldSelection_Unwraps()
    {
        var (text, start, length) = BoldMarkup.ToggleSelection("Jesus **loves** me", 8, 5);
        Assert.Equal("Jesus loves me", text);
        Assert.Equal(6, start);
        Assert.Equal(5, length);
    }
}
