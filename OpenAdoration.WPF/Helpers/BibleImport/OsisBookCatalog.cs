using OpenAdoration.Domain.Enums;

namespace OpenAdoration.WPF.Helpers.BibleImport;

/// <summary>
/// Maps OSIS / USFX book identifiers (e.g. "Gen", "Matt") to canonical
/// English name, abbreviation, canonical number, and testament.
/// Used by both <see cref="OsisXmlParser"/> and <see cref="UsfxXmlParser"/>.
/// </summary>
internal static class OsisBookCatalog
{
    public readonly record struct BookInfo(
        string   Name,
        string   Abbreviation,
        int      Number,
        Testament Testament);

    // Key = OSIS/USFX book ID (case-sensitive as they appear in the files)
    private static readonly Dictionary<string, BookInfo> _map = new()
    {
        // ── Old Testament ────────────────────────────────────────────────────
        ["Gen"]   = new("Genesis",          "Gen",    1, Testament.Old),
        ["Exod"]  = new("Exodus",           "Exod",   2, Testament.Old),
        ["Lev"]   = new("Leviticus",        "Lev",    3, Testament.Old),
        ["Num"]   = new("Numbers",          "Num",    4, Testament.Old),
        ["Deut"]  = new("Deuteronomy",      "Deut",   5, Testament.Old),
        ["Josh"]  = new("Joshua",           "Josh",   6, Testament.Old),
        ["Judg"]  = new("Judges",           "Judg",   7, Testament.Old),
        ["Ruth"]  = new("Ruth",             "Ruth",   8, Testament.Old),
        ["1Sam"]  = new("1 Samuel",         "1Sam",   9, Testament.Old),
        ["2Sam"]  = new("2 Samuel",         "2Sam",  10, Testament.Old),
        ["1Kgs"]  = new("1 Kings",          "1Kgs",  11, Testament.Old),
        ["2Kgs"]  = new("2 Kings",          "2Kgs",  12, Testament.Old),
        ["1Chr"]  = new("1 Chronicles",     "1Chr",  13, Testament.Old),
        ["2Chr"]  = new("2 Chronicles",     "2Chr",  14, Testament.Old),
        ["Ezra"]  = new("Ezra",             "Ezra",  15, Testament.Old),
        ["Neh"]   = new("Nehemiah",         "Neh",   16, Testament.Old),
        ["Esth"]  = new("Esther",           "Esth",  17, Testament.Old),
        ["Job"]   = new("Job",              "Job",   18, Testament.Old),
        ["Ps"]    = new("Psalms",           "Ps",    19, Testament.Old),
        ["Prov"]  = new("Proverbs",         "Prov",  20, Testament.Old),
        ["Eccl"]  = new("Ecclesiastes",     "Eccl",  21, Testament.Old),
        ["Song"]  = new("Song of Solomon",  "Song",  22, Testament.Old),
        ["Isa"]   = new("Isaiah",           "Isa",   23, Testament.Old),
        ["Jer"]   = new("Jeremiah",         "Jer",   24, Testament.Old),
        ["Lam"]   = new("Lamentations",     "Lam",   25, Testament.Old),
        ["Ezek"]  = new("Ezekiel",          "Ezek",  26, Testament.Old),
        ["Dan"]   = new("Daniel",           "Dan",   27, Testament.Old),
        ["Hos"]   = new("Hosea",            "Hos",   28, Testament.Old),
        ["Joel"]  = new("Joel",             "Joel",  29, Testament.Old),
        ["Amos"]  = new("Amos",             "Amos",  30, Testament.Old),
        ["Obad"]  = new("Obadiah",          "Obad",  31, Testament.Old),
        ["Jonah"] = new("Jonah",            "Jon",   32, Testament.Old),
        ["Mic"]   = new("Micah",            "Mic",   33, Testament.Old),
        ["Nah"]   = new("Nahum",            "Nah",   34, Testament.Old),
        ["Hab"]   = new("Habakkuk",         "Hab",   35, Testament.Old),
        ["Zeph"]  = new("Zephaniah",        "Zeph",  36, Testament.Old),
        ["Hag"]   = new("Haggai",           "Hag",   37, Testament.Old),
        ["Zech"]  = new("Zechariah",        "Zech",  38, Testament.Old),
        ["Mal"]   = new("Malachi",          "Mal",   39, Testament.Old),
        // ── New Testament ────────────────────────────────────────────────────
        ["Matt"]   = new("Matthew",          "Matt",   40, Testament.New),
        ["Mark"]   = new("Mark",             "Mark",   41, Testament.New),
        ["Luke"]   = new("Luke",             "Luke",   42, Testament.New),
        ["John"]   = new("John",             "John",   43, Testament.New),
        ["Acts"]   = new("Acts",             "Acts",   44, Testament.New),
        ["Rom"]    = new("Romans",           "Rom",    45, Testament.New),
        ["1Cor"]   = new("1 Corinthians",    "1Cor",   46, Testament.New),
        ["2Cor"]   = new("2 Corinthians",    "2Cor",   47, Testament.New),
        ["Gal"]    = new("Galatians",        "Gal",    48, Testament.New),
        ["Eph"]    = new("Ephesians",        "Eph",    49, Testament.New),
        ["Phil"]   = new("Philippians",      "Phil",   50, Testament.New),
        ["Col"]    = new("Colossians",       "Col",    51, Testament.New),
        ["1Thess"] = new("1 Thessalonians",  "1Thess", 52, Testament.New),
        ["2Thess"] = new("2 Thessalonians",  "2Thess", 53, Testament.New),
        ["1Tim"]   = new("1 Timothy",        "1Tim",   54, Testament.New),
        ["2Tim"]   = new("2 Timothy",        "2Tim",   55, Testament.New),
        ["Titus"]  = new("Titus",            "Titus",  56, Testament.New),
        ["Phlm"]   = new("Philemon",         "Phlm",   57, Testament.New),
        ["Heb"]    = new("Hebrews",          "Heb",    58, Testament.New),
        ["Jas"]    = new("James",            "Jas",    59, Testament.New),
        ["1Pet"]   = new("1 Peter",          "1Pet",   60, Testament.New),
        ["2Pet"]   = new("2 Peter",          "2Pet",   61, Testament.New),
        ["1John"]  = new("1 John",           "1John",  62, Testament.New),
        ["2John"]  = new("2 John",           "2John",  63, Testament.New),
        ["3John"]  = new("3 John",           "3John",  64, Testament.New),
        ["Jude"]   = new("Jude",             "Jude",   65, Testament.New),
        ["Rev"]    = new("Revelation",       "Rev",    66, Testament.New),
    };

    // Reverse lookup: canonical book number (1–66) → BookInfo
    private static readonly Lazy<Dictionary<int, BookInfo>> _byNumber = new(
        () => _map.Values.ToDictionary(b => b.Number));

    /// <summary>Tries to resolve an OSIS/USFX book identifier.</summary>
    public static bool TryGet(string osisId, out BookInfo info)
        => _map.TryGetValue(osisId, out info);

    /// <summary>
    /// Returns canonical info or a generated fallback when the ID is not in the
    /// catalog (handles non-standard abbreviations in some files).
    /// </summary>
    public static BookInfo GetOrFallback(string osisId, int fallbackNumber, string fallbackName)
    {
        if (_map.TryGetValue(osisId, out var info)) return info;

        return new BookInfo(
            Name:         fallbackName.Length > 0 ? fallbackName : osisId,
            Abbreviation: osisId.Length <= 5 ? osisId : osisId[..5],
            Number:       fallbackNumber,
            Testament:    fallbackNumber >= 40 ? Testament.New : Testament.Old);
    }

    /// <summary>
    /// Returns canonical info for a BibleSuperSearch integer book number (1–66).
    /// Falls back to a generated entry for any number outside the canonical range.
    /// </summary>
    public static BookInfo GetByNumber(int bookNumber)
    {
        if (_byNumber.Value.TryGetValue(bookNumber, out var info)) return info;

        return new BookInfo(
            Name:         $"Book {bookNumber}",
            Abbreviation: $"Bk{bookNumber}",
            Number:       bookNumber,
            Testament:    bookNumber >= 40 ? Testament.New : Testament.Old);
    }
}
