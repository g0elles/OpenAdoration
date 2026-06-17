namespace OpenAdoration.Plugins.Abstractions;

/// <summary>Testament marker — mirrors the host's domain enum without referencing it.</summary>
public enum PluginTestament { Old, New }

public sealed record PluginBibleVersionInfo(string Id, string Name, string Abbreviation, string Language);

public sealed record PluginBibleBook(string Name, string Abbreviation, int Number, PluginTestament Testament, int ChapterCount);

public sealed record PluginBibleVerse(string Book, int Chapter, int Verse, string Text);

/// <summary>
/// A fetched version: its metadata, books, and verses. The host maps this onto its domain
/// entities and feeds <c>IBibleService.UpsertVersionVersesAsync</c>.
/// </summary>
public sealed record PluginBibleData(
    PluginBibleVersionInfo Version,
    IReadOnlyList<PluginBibleBook> Books,
    IReadOnlyList<PluginBibleVerse> Verses);
