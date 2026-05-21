using OpenAdoration.Application.Common;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public interface IMediaService
{
    Task<IReadOnlyList<MediaFile>> GetAllAsync(CancellationToken ct = default);
    Task<MediaFile?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<MediaFile> AddAsync(MediaFile file, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Generates a media slide from a registered file.
    /// Pass <paramref name="themeId"/> to override the default theme on the generated slide.
    /// When null, the default theme is used.
    /// </summary>
    Slide GenerateSlide(MediaFile file, int? themeId = null);
}
