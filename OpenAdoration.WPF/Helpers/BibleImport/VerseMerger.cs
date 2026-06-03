using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>
/// Accumulates verses while collapsing repeated (Book, Chapter, Verse) keys into a
/// single <see cref="BibleVerse"/>, joining their text with a separating space.
///
/// Bible exchange formats (Zefania, OSIS, USFX) routinely emit one logical verse as
/// several elements that share a verse number (split / poetry verses). Left as-is,
/// those collide on the unique (BibleVersionId, Book, Chapter, Verse) index and abort
/// the entire import — so every XML parser feeds its verses through this merger.
/// </summary>
internal sealed class VerseMerger
{
    private readonly Dictionary<(string Book, int Chapter, int Verse), BibleVerse> _byKey = new();
    private readonly List<BibleVerse> _verses = new();

    /// <summary>The merged verses, in first-seen order.</summary>
    public List<BibleVerse> Verses => _verses;

    /// <summary>Adds a verse, or appends its text to the matching existing entry.</summary>
    public void Add(string book, int chapter, int verse, string text)
    {
        var key = (book, chapter, verse);
        if (_byKey.TryGetValue(key, out var existing))
        {
            existing.Text = $"{existing.Text} {text}";
            return;
        }

        var entry = new BibleVerse { Book = book, Chapter = chapter, Verse = verse, Text = text };
        _byKey[key] = entry;
        _verses.Add(entry);
    }
}
