using OpenAdoration.Application.Common;
using OpenAdoration.WPF.ViewModels;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Stage;

/// <summary>
/// F3: the stage view's clickable "all slides" list (StageView.xaml, ALL SLIDES pane) is built by
/// <see cref="StageViewModel.BuildSlideListItems"/> from the same slides/current-index already read
/// in RefreshAsync. Split out as a pure static method (mirrors <see cref="StageViewModel.ComputeMirrorScale"/>)
/// so it's testable without a live WPF Dispatcher/Application.Current.
/// </summary>
public sealed class StageViewModelSlideListTests
{
    private static Slide MakeSlide(string label, string content) =>
        new(content, SlideType.Song, label);

    [Fact]
    public void BuildSlideListItems_MarksOnlyCurrentIndex()
    {
        var slides = new[]
        {
            MakeSlide("Verse 1", "Amazing grace"),
            MakeSlide("Chorus", "How sweet the sound"),
            MakeSlide("Verse 2", "T'was grace that taught"),
        };

        var items = StageViewModel.BuildSlideListItems(slides, currentIndex: 1);

        Assert.Equal(3, items.Count);
        Assert.False(items[0].IsCurrent);
        Assert.True(items[1].IsCurrent);
        Assert.False(items[2].IsCurrent);
        Assert.Equal("Chorus", items[1].Label);
        Assert.Equal(0, items[0].Index);
        Assert.Equal(2, items[2].Index);
    }

    [Fact]
    public void BuildSlideListItems_EmptySlideList_ReturnsEmpty()
    {
        var items = StageViewModel.BuildSlideListItems(Array.Empty<Slide>(), currentIndex: -1);

        Assert.Empty(items);
    }

    [Fact]
    public void BuildSlideListItems_LongContent_TruncatesFirstLineWithEllipsis()
    {
        var longLine = new string('x', 60);
        var slides = new[] { MakeSlide("Verse 1", longLine + "\nsecond line") };

        var items = StageViewModel.BuildSlideListItems(slides, currentIndex: 0);

        Assert.EndsWith("…", items[0].PreviewText);
        Assert.True(items[0].PreviewText.Length <= 41);
        Assert.DoesNotContain("second line", items[0].PreviewText);
    }

    [Fact]
    public void BuildSlideListItems_ShortContent_NotTruncated()
    {
        var slides = new[] { MakeSlide("Verse 1", "Short line") };

        var items = StageViewModel.BuildSlideListItems(slides, currentIndex: 0);

        Assert.Equal("Short line", items[0].PreviewText);
    }
}
