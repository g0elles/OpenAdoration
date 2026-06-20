using System.Windows;
using System.Windows.Threading;
using OpenAdoration.WPF.Helpers;
using Image = System.Windows.Controls.Image;

namespace OpenAdoration.WPF.Behaviors;

/// <summary>
/// Attach to an <see cref="Image"/> to show a thumbnail for a media file path:
/// <c>behaviors:ThumbnailImage.Source="{Binding FilePath}"</c>.
///
/// Resolution order: disk cache → Windows shell thumbnail (fast, sync; images + most videos) →
/// FFmpeg frame grab on a background thread (codecs the OS can't thumbnail, e.g. HEVC .MOV),
/// whose result is cached to disk so it's instant next time. The UI never blocks on FFmpeg.
/// </summary>
public static class ThumbnailImage
{
    private const int Size = 256;

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.RegisterAttached(
            "Source", typeof(string), typeof(ThumbnailImage),
            new PropertyMetadata(null, OnSourceChanged));

    public static void SetSource(DependencyObject o, string? value) => o.SetValue(SourceProperty, value);
    public static string? GetSource(DependencyObject o) => (string?)o.GetValue(SourceProperty);

    private static void OnSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not Image image) return;
        var path = e.NewValue as string;
        image.Source = null;
        if (string.IsNullOrWhiteSpace(path)) return;

        var cached = ThumbnailCache.TryLoad(path) ?? ShellThumbnail.TryGet(path, Size);
        if (cached is not null)
        {
            image.Source = cached;
            return;
        }

        // OS can't thumbnail it (e.g. HEVC) — decode a frame off-thread, then apply if still relevant.
        System.Threading.Tasks.Task.Run(() =>
        {
            var bmp = FfmpegThumbnail.TryExtract(path, Size);
            if (bmp is null) return;
            ThumbnailCache.Save(path, bmp);
            image.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (GetSource(image) == path) image.Source = bmp; // guard against recycled containers
            });
        });
    }
}
