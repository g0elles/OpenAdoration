using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.ViewModels;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Media;

/// <summary>
/// F2 (operator feedback): standalone media projection should feed Stage View's "up next" pane,
/// mirroring the cross-item preview hint ServiceScheduleViewModel already provides during a live
/// service. <see cref="MediaViewModel.FindNextMediaFile"/> is the pure list-index part of that
/// logic (given the currently displayed file list — respecting whichever of Multimedia/Fondos
/// the operator is looking at — and the file just projected, what's next) — pulled out so it's
/// unit-testable without standing up the DI-heavy ViewModel, mirroring the
/// StageViewModel.ComputeMirrorScale/BuildSlideListItems pattern.
/// </summary>
public sealed class MediaViewModelFindNextMediaFileTests
{
    private static MediaFile MakeFile(int id, string name) => new() { Id = id, FileName = name };

    [Fact]
    public void FindNextMediaFile_CurrentInMiddle_ReturnsFollowingFile()
    {
        var files = new List<MediaFile> { MakeFile(1, "a.jpg"), MakeFile(2, "b.jpg"), MakeFile(3, "c.jpg") };

        var next = MediaViewModel.FindNextMediaFile(files, files[0]);

        Assert.Same(files[1], next);
    }

    [Fact]
    public void FindNextMediaFile_CurrentIsLast_ReturnsNull()
    {
        var files = new List<MediaFile> { MakeFile(1, "a.jpg"), MakeFile(2, "b.jpg") };

        var next = MediaViewModel.FindNextMediaFile(files, files[^1]);

        Assert.Null(next);
    }

    [Fact]
    public void FindNextMediaFile_CurrentNotInList_ReturnsNull()
    {
        var files = new List<MediaFile> { MakeFile(1, "a.jpg"), MakeFile(2, "b.jpg") };
        var missing = MakeFile(99, "not-displayed.jpg");

        var next = MediaViewModel.FindNextMediaFile(files, missing);

        Assert.Null(next);
    }

    [Fact]
    public void FindNextMediaFile_MatchesById_NotReferenceEquality()
    {
        var files = new List<MediaFile> { MakeFile(1, "a.jpg"), MakeFile(2, "b.jpg") };
        var sameIdDifferentInstance = MakeFile(1, "a.jpg");

        var next = MediaViewModel.FindNextMediaFile(files, sameIdDifferentInstance);

        Assert.Same(files[1], next);
    }
}
