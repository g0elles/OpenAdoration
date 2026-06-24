using OpenAdoration.Application.Common;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public interface IMediaService
{
    /// <summary>General (non-background) media, projectable as slides.</summary>
    Task<IReadOnlyList<MediaFile>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Background media (theme backgrounds), the exclusive other category.</summary>
    Task<IReadOnlyList<MediaFile>> GetBackgroundsAsync(CancellationToken ct = default);
    Task<MediaFile?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<MediaFile?> GetByContentHashAsync(string contentHash, bool isBackground = false, CancellationToken ct = default);
    Task<MediaFile> AddAsync(MediaFile file, CancellationToken ct = default);

    /// <summary>
    /// Copies <paramref name="sourcePath"/> into the managed media store as a reusable
    /// background (deduped by content), returning the stored record. Reuses an existing
    /// background with identical bytes instead of duplicating the file.
    /// </summary>
    Task<MediaFile> ImportBackgroundAsync(string sourcePath, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Generates a media slide from a registered file.
    /// Pass <paramref name="themeId"/> to override the default theme on the generated slide.
    /// When null, the default theme is used.
    /// </summary>
    Slide GenerateSlide(MediaFile file, int? themeId = null);
}
