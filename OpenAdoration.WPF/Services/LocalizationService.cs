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
    private static readonly LanguageOption[] Languages =
    {
        new("en", "English"),
        new("es", "Español"),
    };

    public IReadOnlyList<LanguageOption> AvailableLanguages => Languages;

    public string CurrentLanguageCode =>
        TranslationSource.Instance.CurrentCulture.TwoLetterISOLanguageName;

    public LocalizationService(IAppSettingsService settings)
    {
        Apply(ResolveStartupCode(settings.Current.UiCulture));
    }

    public void SetLanguage(string code) =>
        Apply(IsSupported(code) ? code : "en");

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
