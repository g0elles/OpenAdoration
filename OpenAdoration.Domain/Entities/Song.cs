using OpenAdoration.Domain.Common;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.Domain.Entities;

public class Song : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Classification { get; set; }
    public string? Copyright { get; set; }
    public string? CcliNumber { get; set; }

    /// <summary>
    /// Stable cross-file identity from the import source (VideoPsalm song <c>Guid</c>).
    /// Used to dedup the same song imported from multiple <c>.vpagd</c> agendas.
    /// Null for songs created in-app.
    /// </summary>
    public string? SourceGuid { get; set; }

    /// <summary>
    /// Space-separated section token sequence, e.g. "V1 C V2 C B C".
    /// When null/empty the definition order (Sections.Order) is used.
    /// Tokens: V{n}=Verse, C{n}=Chorus, P{n}=PreChorus, B{n}=Bridge, I=Intro, O=Outro, T=Tag.
    /// </summary>
    public string? VerseOrder { get; set; }

    public List<SongSection> Sections { get; set; } = new();

    /// <summary>
    /// Returns the song's sections in projection order.
    /// Pass <paramref name="verseOrderOverride"/> to use a per-service order token string
    /// instead of the song's own <see cref="VerseOrder"/>. When both are null/empty the
    /// raw definition order (<see cref="SongSection.Order"/>) is used.
    /// </summary>
    public IReadOnlyList<SongSection> GetOrderedSections(string? verseOrderOverride = null)
    {
        var definitionOrder = Sections.OrderBy(s => s.Order).ToList();

        var order = string.IsNullOrWhiteSpace(verseOrderOverride) ? VerseOrder : verseOrderOverride;

        if (string.IsNullOrWhiteSpace(order))
            return definitionOrder;

        var resolved = order
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(ResolveToken)
            .Where(r => r.HasValue)
            .Select(r => definitionOrder.FirstOrDefault(
                s => s.Type == r!.Value.type && s.SectionNumber == r!.Value.number))
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

        return resolved.Count > 0 ? resolved : definitionOrder;
    }

    private static (SectionType type, int number)? ResolveToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;

        var prefix = new string(token.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        var numPart = new string(token.SkipWhile(char.IsLetter).ToArray());
        var number = int.TryParse(numPart, out var n) ? n : 1;

        return prefix switch
        {
            "V" => (SectionType.Verse,     number),
            "C" => (SectionType.Chorus,    number),
            "P" => (SectionType.PreChorus, number),
            "B" => (SectionType.Bridge,    number),
            "I" => (SectionType.Intro,     1),
            "O" => (SectionType.Outro,     1),
            "T" => (SectionType.Tag,       1),
            _   => null
        };
    }
}
