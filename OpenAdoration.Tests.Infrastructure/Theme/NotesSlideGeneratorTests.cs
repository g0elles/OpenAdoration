using OpenAdoration.Application.Common;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Theming;

public sealed class NotesSlideGeneratorTests
{
    [Fact]
    public void GenerateSlides_SingleParagraph_OneSlide()
    {
        var slides = NotesSlideGenerator.GenerateSlides("Just one paragraph.");

        var slide = Assert.Single(slides);
        Assert.Equal("Just one paragraph.", slide.Content);
        Assert.Equal("1", slide.Label);
        Assert.Equal(SlideType.Notes, slide.Type);
    }

    [Fact]
    public void GenerateSlides_BlankLineSeparatedParagraphs_OneSlidePerParagraph()
    {
        var slides = NotesSlideGenerator.GenerateSlides("First point.\n\nSecond point.\n\nThird point.");

        Assert.Equal(3, slides.Count);
        Assert.Equal("First point.", slides[0].Content);
        Assert.Equal("Second point.", slides[1].Content);
        Assert.Equal("Third point.", slides[2].Content);
    }

    [Fact]
    public void GenerateSlides_LeadingTrailingBlankLines_AreIgnored()
    {
        var slides = NotesSlideGenerator.GenerateSlides("\n\n\nOnly point.\n\n\n");

        var slide = Assert.Single(slides);
        Assert.Equal("Only point.", slide.Content);
    }

    [Fact]
    public void GenerateSlides_EmptyOrWhitespaceContent_ReturnsNoSlides()
    {
        Assert.Empty(NotesSlideGenerator.GenerateSlides(""));
        Assert.Empty(NotesSlideGenerator.GenerateSlides("   \n\n  "));
    }

    [Fact]
    public void GenerateSlides_PassesThemeIdToEverySlide()
    {
        var slides = NotesSlideGenerator.GenerateSlides("One\n\nTwo", themeId: 42);

        Assert.All(slides, s => Assert.Equal(42, s.ThemeId));
    }
}
