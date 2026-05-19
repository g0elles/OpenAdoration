using OpenAdoration.Domain.Entities;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>Parsed output shared by every format parser.</summary>
public sealed record BibleImportResult(
    BibleVersion     Version,
    List<BibleBook>  Books,
    List<BibleVerse> Verses);
