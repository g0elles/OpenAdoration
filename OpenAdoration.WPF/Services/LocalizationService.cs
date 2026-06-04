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
    // Temporary lock to English until the UI is fully translated (M11). While false, the
    // app ignores saved/OS culture and offers only English, so a partially-translated UI
    // is never shown. Flip to true once every view/dialog/VM string is localized.
    private const bool MultiLanguageEnabled = false;

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
