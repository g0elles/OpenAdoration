namespace OpenAdoration.Application.Common;

/// <summary>How a Bible full-text search term is interpreted.</summary>
public enum BibleSearchMode
{
    /// <summary>All words must appear (order-independent), with prefix matching per word.</summary>
    Keyword,

    /// <summary>The words must appear together as an exact phrase.</summary>
    Phrase
}
