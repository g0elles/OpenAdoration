using System.Linq;
using System.Windows;
using OpenAdoration.Application.Common;

namespace OpenAdoration.WPF.Services;

/// <summary>Swaps the app-chrome colour palette (Light/Dark) at runtime.</summary>
public interface IAppThemeService
{
    /// <summary>Replaces the merged palette dictionary; DynamicResource consumers re-resolve live.</summary>
    void Apply(AppearanceMode mode);
}

public sealed class AppThemeService : IAppThemeService
{
    public void Apply(AppearanceMode mode)
    {
        var dicts = System.Windows.Application.Current.Resources.MergedDictionaries;
        var next = new ResourceDictionary
        {
            Source = new System.Uri($"Styles/Colors.{mode}.xaml", System.UriKind.Relative)
        };

        // The palette dict is the only merged one defining the brush keys.
        var existing = dicts.FirstOrDefault(d => d.Contains("PrimaryBrush"));
        if (existing is null)
        {
            dicts.Insert(0, next);
            return;
        }
        dicts[dicts.IndexOf(existing)] = next; // in-place replace keeps merge order
    }
}
