using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Repositories;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;

namespace OpenAdoration.Application.Services;

public sealed class MediaService : IMediaService
{
    private readonly IMediaRepository _repository;
    private readonly AppPaths _appPaths;
    private readonly ILogger<MediaService> _logger;

    public MediaService(IMediaRepository repository, AppPaths appPaths, ILogger<MediaService> logger)
    {
        _repository = repository;
        _appPaths = appPaths;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MediaFile>> GetAllAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching all media files");
        return await _repository.GetAllAsync(ct);
    }

    public async Task<IReadOnlyList<MediaFile>> GetBackgroundsAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching background media files");
        return await _repository.GetBackgroundsAsync(ct);
    }

    public async Task<MediaFile?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _logger.LogDebug("Fetching media file {MediaId}", id);
        return await _repository.GetByIdAsync(id, ct);
    }

    public async Task<MediaFile?> GetByContentHashAsync(string contentHash, bool isBackground = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        return await _repository.GetByContentHashAsync(contentHash, isBackground, ct);
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

    public async Task<MediaFile> ImportBackgroundAsync(string sourcePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Background source file not found.", sourcePath);
        if (!MediaFormats.IsSupported(sourcePath))
            throw new NotSupportedException($"Unsupported background format: {Path.GetExtension(sourcePath)}");

        // Dedup by content within the background category: re-importing the same file (or one
        // already shared by another theme) reuses the stored copy instead of duplicating it.
        var hash = ComputeHash(sourcePath);
        if (await _repository.GetByContentHashAsync(hash, isBackground: true, ct) is { } existing)
        {
            _logger.LogInformation("Reusing existing background {MediaId}: {FileName}", existing.Id, existing.FileName);
            return existing;
        }

        Directory.CreateDirectory(_appPaths.MediaDirectory);
        var destPath = UniqueDestination(_appPaths.MediaDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destPath);

        return await AddAsync(new MediaFile
        {
            FileName     = Path.GetFileName(destPath),
            FilePath     = destPath,
            Type         = MediaFormats.IsVideo(sourcePath) ? MediaType.Video : MediaType.Image,
            ContentHash  = hash,
            IsBackground = true
        }, ct);
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

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    // Avoid clobbering an existing store file with the same name (different content).
    private static string UniqueDestination(string directory, string fileName)
    {
        var destPath = Path.Combine(directory, fileName);
        if (!File.Exists(destPath)) return destPath;

        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext  = Path.GetExtension(fileName);
        var n    = 1;
        do { destPath = Path.Combine(directory, $"{name} ({n++}){ext}"); }
        while (File.Exists(destPath));
        return destPath;
    }
}
