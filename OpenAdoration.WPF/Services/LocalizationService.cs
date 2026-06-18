using System.Globalization;
using System.Threading;
using OpenAdoration.Application.Services;
using OpenAdoration.WPF.Localization;

namespace OpenAdoration.WPF.Services;

/// <summary>
/// Drives the active UI language: updates <see cref="TranslationSource"/> and the
/// thread UI culture. Resolves the startup language from saved settings, falling back
/// to the OS language when supported, otherwise English.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    // M11.3 complete: every view, dialog, and VM string is externalized to resx (en + es,
    // parity-tested). Multi-language is live — the app honours saved/OS culture and the
    // Settings language picker. Re-lock to English only if a future view ships untranslated.
    private const bool MultiLanguageEnabled = true;

    private static readonly LanguageOption[] Languages =
    {
        new("en", "English"),
        new("es", "Español"),
    };

    public IReadOnlyList<LanguageOption> AvailableLanguages =>
        MultiLanguageEnabled ? Languages : Languages.Where(l => l.Code == "en").ToList();

    public string CurrentLanguageCode =>
        TranslationSource.Instance.CurrentCulture.TwoLetterISOLanguageName;

    public LocalizationService(IAppSettingsService settings)
    {
        Apply(MultiLanguageEnabled ? ResolveStartupCode(settings.Current.UiCulture) : "en");
    }

    public void SetLanguage(string code) =>
        Apply(MultiLanguageEnabled && IsSupported(code) ? code : "en");

    private static void Apply(string code)
    {
        var culture = CultureInfo.GetCultureInfo(code);
        TranslationSource.Instance.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture     = culture;
    }

    private static string ResolveStartupCode(string? saved)
    {
        if (IsSupported(saved)) return saved!;
        var os = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return IsSupported(os) ? os : "en";
    }

    private static bool IsSupported(string? code) =>
        !string.IsNullOrWhiteSpace(code) && Languages.Any(l => l.Code == code);
}
