using Microsoft.Extensions.Logging.Abstractions;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Bible;

// GenerateSlide(s) is pure (no repository access), so it's exercised directly. (M10.3)
public class DualVersionSlideTests
{
    private static BibleService Service() => new(null!, NullLogger<BibleService>.Instance);

    private static BibleVerse V(int n, string text) =>
        new() { Book = "John", Chapter = 3, Verse = n, Text = text };

    [Fact]
    public void DualVersion_StacksBothBlocks_AndLabelsBothVersions()
    {
        var primary   = new[] { V(1, "Primary one"), V(2, "Primary two") };
        var secondary = new[] { V(1, "Secondary one"), V(2, "Secondary two") };

        var slides = Service().GenerateSlides(primary, versesPerSlide: 2,
            version: new BibleVersion { Name = "KJV" },
            secondaryVerses: secondary, secondaryVersion: new BibleVersion { Name = "RVR" });

        var slide = Assert.Single(slides);
        Assert.Contains("Primary one", slide.Content);
        Assert.Contains("Secondary two", slide.Content);
        Assert.Equal("KJV · RVR", slide.Context.BibleDescription);
        Assert.True(slide.Content.IndexOf("Primary one") < slide.Content.IndexOf("Secondary one"),
            "primary block should precede the secondary block");
    }

    [Fact]
    public void DualVersion_VersificationGap_PairsOnlyMatchingVerseNumbers()
    {
        var primary   = new[] { V(1, "P1"), V(2, "P2"), V(3, "P3") };
        var secondary = new[] { V(1, "S1"), V(3, "S3") }; // secondary has no verse 2

        var slides = Service().GenerateSlides(primary, versesPerSlide: 3,
            version: new BibleVersion { Name = "A" },
            secondaryVerses: secondary, secondaryVersion: new BibleVersion { Name = "B" });

        var slide = Assert.Single(slides);
        Assert.Contains("S1", slide.Content);
        Assert.Contains("S3", slide.Content);
        Assert.DoesNotContain("S2", slide.Content);
    }

    [Fact]
    public void NoSecondary_FallsBackToSingleVersionDescription()
    {
        var slides = Service().GenerateSlides(new[] { V(1, "Only") }, versesPerSlide: 1,
            version: new BibleVersion { Name = "KJV" });

        Assert.Equal("KJV", Assert.Single(slides).Context.BibleDescription);
    }
}
