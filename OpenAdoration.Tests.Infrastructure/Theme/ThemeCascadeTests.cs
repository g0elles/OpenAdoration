using OpenAdoration.Application.Common;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Theme;

public sealed class ThemeCascadeTests
{
    private static AppSettings Settings(int? song = null, int? scripture = null, int? media = null) =>
        new() { DefaultSongThemeId = song, DefaultScriptureThemeId = scripture, DefaultMediaThemeId = media };

    [Fact]
    public void ForSong_PrefersScheduleItem_ThenSong_ThenDefault_ThenNull()
    {
        var s = Settings(song: 3);
        Assert.Equal(1, ThemeCascade.ForSong(1, 2, s));       // schedule item wins
        Assert.Equal(2, ThemeCascade.ForSong(null, 2, s));    // falls to song
        Assert.Equal(3, ThemeCascade.ForSong(null, null, s)); // falls to content-type default
        Assert.Null(ThemeCascade.ForSong(null, null, Settings())); // null = app default downstream
    }

    [Fact]
    public void ForScripture_PrefersScheduleItem_ThenDefault_ThenNull()
    {
        var s = Settings(scripture: 5);
        Assert.Equal(4, ThemeCascade.ForScripture(4, s));
        Assert.Equal(5, ThemeCascade.ForScripture(null, s));
        Assert.Null(ThemeCascade.ForScripture(null, Settings()));
    }

    [Fact]
    public void ForMedia_PrefersScheduleItem_ThenDefault_ThenNull()
    {
        var s = Settings(media: 7);
        Assert.Equal(6, ThemeCascade.ForMedia(6, s));
        Assert.Equal(7, ThemeCascade.ForMedia(null, s));
        Assert.Null(ThemeCascade.ForMedia(null, Settings()));
    }
}
