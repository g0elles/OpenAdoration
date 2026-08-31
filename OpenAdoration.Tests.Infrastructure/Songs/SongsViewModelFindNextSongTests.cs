using OpenAdoration.Domain.Entities;
using OpenAdoration.WPF.ViewModels;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Songs;

/// <summary>
/// F2 (operator feedback): standalone song projection should feed Stage View's "up next" pane,
/// mirroring the cross-item preview hint ServiceScheduleViewModel already provides during a live
/// service. <see cref="SongsViewModel.FindNextSong"/> is the pure list-index part of that logic
/// (given the displayed song list and the one just projected, what's next) — pulled out so it's
/// unit-testable without standing up the DI-heavy ViewModel, mirroring the
/// StageViewModel.ComputeMirrorScale/BuildSlideListItems pattern.
/// </summary>
public sealed class SongsViewModelFindNextSongTests
{
    private static Song MakeSong(int id, string title) => new() { Id = id, Title = title };

    [Fact]
    public void FindNextSong_CurrentInMiddle_ReturnsFollowingSong()
    {
        var songs = new List<Song> { MakeSong(1, "A"), MakeSong(2, "B"), MakeSong(3, "C") };

        var next = SongsViewModel.FindNextSong(songs, songs[0]);

        Assert.Same(songs[1], next);
    }

    [Fact]
    public void FindNextSong_CurrentIsLast_ReturnsNull()
    {
        var songs = new List<Song> { MakeSong(1, "A"), MakeSong(2, "B") };

        var next = SongsViewModel.FindNextSong(songs, songs[^1]);

        Assert.Null(next);
    }

    [Fact]
    public void FindNextSong_CurrentNotInList_ReturnsNull()
    {
        var songs = new List<Song> { MakeSong(1, "A"), MakeSong(2, "B") };
        var missing = MakeSong(99, "Not Displayed");

        var next = SongsViewModel.FindNextSong(songs, missing);

        Assert.Null(next);
    }

    [Fact]
    public void FindNextSong_MatchesById_NotReferenceEquality()
    {
        // Song search re-runs GenerateSlides on a fetched entity, not necessarily the same
        // object instance held by the displayed collection — the lookup must key off Id.
        var songs = new List<Song> { MakeSong(1, "A"), MakeSong(2, "B") };
        var sameIdDifferentInstance = MakeSong(1, "A");

        var next = SongsViewModel.FindNextSong(songs, sameIdDifferentInstance);

        Assert.Same(songs[1], next);
    }
}
