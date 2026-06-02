namespace OpenAdoration.WPF.Services;

/// <summary>A selectable UI language: ISO code (e.g. "es") + its name in that language.</summary>
public sealed record LanguageOption(string Code, string NativeName);

/// <summary>
/// Applies and reports the active UI language. Changing the language updates every
/// localized binding live. Implemented by the WPF layer (UI concern).
/// </summary>
public interface ILocalizationService
{
    IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    /// <summary>Two-letter ISO code of the active language (e.g. "en", "es").</summary>
    string CurrentLanguageCode { get; }

    /// <summary>Applies a language by code; unsupported codes fall back to English.</summary>
    void SetLanguage(string code);
}
