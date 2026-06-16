using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;

namespace OpenAdoration.Application.Services;

public sealed class MediaService : IMediaService
{
    private readonly IMediaRepository _repository;
    private readonly ILogger<MediaService> _logger;

    public MediaService(IMediaRepository repository, ILogger<MediaService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MediaFile>> GetAllAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching all media files");
        return await _repository.GetAllAsync(ct);
    }

    public async Task<MediaFile?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching media file {MediaId}", id);
        return await _repository.GetByIdAsync(id, ct);
    }

    public async Task<MediaFile?> GetByContentHashAsync(string contentHash, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        return await _repository.GetByContentHashAsync(contentHash, ct);
    }

    public async Task<MediaFile> AddAsync(MediaFile file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        _logger.LogInformation("Adding media file: {FileName} ({Type})", file.FileName, file.Type);

        try
        {
            var added = await _repository.AddAsync(file, ct);
            _logger.LogInformation("Media file added with ID {MediaId}: {FileName}", added.Id, added.FileName);
            return added;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add media file: {FileName}", file.FileName);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting media file {MediaId}", id);

        try
        {
            await _repository.DeleteAsync(id, ct);
            _logger.LogInformation("Media file {MediaId} deleted", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete media file {MediaId}", id);
            throw;
        }
    }

    public Slide GenerateSlide(MediaFile file, int? themeId = null)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!File.Exists(file.FilePath))
            // Log filename only -- full local paths expose usernames/folder structure in support logs.
            _logger.LogWarning("Media file {MediaId} not found on disk: {FileName}", file.Id, Path.GetFileName(file.FilePath));

        return new Slide(
            content: file.FilePath,
            type: SlideType.Media,
            label: file.FileName,
            mediaPath: file.FilePath,
            themeId: themeId);
    }
}
