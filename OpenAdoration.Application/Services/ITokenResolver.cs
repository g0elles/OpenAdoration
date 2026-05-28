using OpenAdoration.Application.Common;

namespace OpenAdoration.Application.Services;

/// <summary>
/// Resolves VP-style template tokens (e.g. [SongTitle], [BibleBookName]) against
/// the current slide context. Used by Tier 2 to render header/footer zone text.
/// </summary>
public interface ITokenResolver
{
    /// <summary>
    /// Replaces all recognized tokens in <paramref name="template"/> with values
    /// derived from <paramref name="context"/>. Unknown tokens are left as-is.
    /// </summary>
    string Resolve(string template, SlideContext context);
}
