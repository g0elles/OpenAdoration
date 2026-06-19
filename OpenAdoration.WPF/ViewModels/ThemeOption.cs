namespace OpenAdoration.WPF.ViewModels;

/// <summary>
/// One entry in a theme picker. <see cref="Id"/> is null for the "inherit / use default" sentinel
/// (which maps to a null <c>ThemeId</c> in the cascade — see <c>ThemeCascade</c>).
/// </summary>
public sealed record ThemeOption(int? Id, string DisplayName)
{
    // Drives the UI-Automation Name for combo items (screen readers); the record default
    // would announce the whole "ThemeOption { Id = .. }" struct instead of the label.
    public override string ToString() => DisplayName;
}
