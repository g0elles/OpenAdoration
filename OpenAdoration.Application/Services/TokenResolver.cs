using System.Text.RegularExpressions;
using OpenAdoration.Application.Common;

namespace OpenAdoration.Application.Services;

public sealed partial class TokenResolver : ITokenResolver
{
    // Matches [AnyToken] — same bracket convention as VideoPsalm.
    [GeneratedRegex(@"\[(\w+)\]", RegexOptions.Compiled)]
    private static partial Regex TokenPattern();

    public string Resolve(string template, SlideContext context)
    {
        if (string.IsNullOrEmpty(template)) return template;

        return TokenPattern().Replace(template, m =>
        {
            return m.Groups[1].Value switch
            {
                "SongTitle"       => context.SongTitle       ?? string.Empty,
                "SongAuthor"      => context.SongAuthor      ?? string.Empty,
                "SongVerseTag"    => context.SongVerseTag    ?? string.Empty,
                "BibleBookName"    => context.BibleBookName    ?? string.Empty,
                "BibleChapterID"  => context.BibleChapterId  ?? string.Empty,
                "BibleVerseID"    => context.BibleVerseId    ?? string.Empty,
                "BibleReference"  => context.BibleReference  ?? string.Empty,
                "BibleDescription"=> context.BibleDescription ?? string.Empty,
                _                 => m.Value  // leave unknown tokens unchanged
            };
        });
    }
}
