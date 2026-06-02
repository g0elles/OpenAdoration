using System.Globalization;
using System.Resources;
using OpenAdoration.WPF.Localization;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Localization;

public sealed class LocalizationResourceTests
{
    private static readonly ResourceManager Strings =
        new("OpenAdoration.WPF.Resources.Strings", typeof(TranslationSource).Assembly);

    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es");

    [Fact]
    public void EnglishResources_Resolve()
    {
        Assert.Equal("Stop", Strings.GetString("Projection_Stop", En));
        Assert.Equal("Settings", Strings.GetString("Settings_Title", En));
    }

    [Fact]
    public void SpanishSatellite_Resolves_AndDiffersFromEnglish()
    {
        Assert.Equal("Detener", Strings.GetString("Projection_Stop", Es));
        Assert.Equal("Configuración", Strings.GetString("Settings_Title", Es));
    }

    [Theory]
    [InlineData("Nav_Songs")]
    [InlineData("Nav_Settings")]
    [InlineData("About_Tagline")]
    [InlineData("Settings_Language")]
    [InlineData("Projection_OpenScreen")]
    public void KeysAreTranslated_InBothLanguages(string key)
    {
        var en = Strings.GetString(key, En);
        var es = Strings.GetString(key, Es);
        Assert.False(string.IsNullOrWhiteSpace(en));
        Assert.False(string.IsNullOrWhiteSpace(es));
        Assert.NotEqual(en, es);
    }
}
