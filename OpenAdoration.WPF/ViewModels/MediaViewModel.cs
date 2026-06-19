using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;
using OpenAdoration.WPF.Helpers;

namespace OpenAdoration.WPF.ViewModels;

public partial class MediaViewModel : BaseViewModel
{
    private readonly IMediaService       _mediaService;
    private readonly IProjectionService  _projectionService;
    private readonly ILogger<MediaViewModel> _logger;

    private static readonly string MediaStore =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenAdoration", "Media");

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };

    private static readonly HashSet<string> VideoExtensions =
        new(StringComparer.OrdinalIgnoreCase)
            { ".mp4", ".avi", ".wmv", ".mov", ".mkv", ".m4v" };

    private static readonly HashSet<string> AllowedExtensions =
        new(ImageExtensions.Concat(VideoExtensions), StringComparer.OrdinalIgnoreCase);

    private const long MaxMediaFileSizeBytes = 1L * 1024 * 1024 * 1024; // 1 GB

    [ObservableProperty] private ObservableCollection<MediaFile> _mediaFiles = new();
    [ObservableProperty] private MediaFile? _selectedFile;

    public bool HasMedia => MediaFiles.Count > 0;

    public MediaViewModel(
        IMediaService       mediaService,
        IProjectionService  projectionService,
        ILogger<MediaViewModel> logger)
    {
        _mediaService      = mediaService;
        _projectionService = projectionService;
        _logger            = logger;
    }

    // ── Load ──────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            await LoadCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load media files");
            SetError(L("Sched_ErrLoadMedia"));
        }
        finally { IsBusy = false; }
    }

    private async Task LoadCoreAsync()
    {
        var files = await _mediaService.GetAllAsync();
        MediaFiles.Clear();
        foreach (var f in files) MediaFiles.Add(f);
        OnPropertyChanged(nameof(HasMedia));
    }

    // ── Import ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ImportFileAsync()
    {
        // G1: Microsoft.Win32.OpenFileDialog — not System.Windows.Forms.OpenFileDialog
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title       = L("Media_ImportTitle"),
            Filter      = L("Media_FilterImages") + "|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.webp" +
                          "|" + L("Media_FilterVideos") + "|*.mp4;*.avi;*.wmv;*.mov;*.mkv;*.m4v" +
                          "|" + L("Media_FilterAll") + "|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.webp;*.mp4;*.avi;*.wmv;*.mov;*.mkv;*.m4v",
            FilterIndex = 3,
            Multiselect = true
        };

        if (dlg.ShowDialog() != true) return;
        await ImportPathsAsync(dlg.FileNames);
    }

    [RelayCommand]
    private async Task ImportFolderAsync()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = L("Media_ImportFolderTitle") };
        if (dlg.ShowDialog() != true) return;

        // Top-level only; the per-file loop filters out non-media by extension/signature.
        var paths = Directory.EnumerateFiles(dlg.FolderName, "*", SearchOption.TopDirectoryOnly).ToList();
        await ImportPathsAsync(paths);
    }

    private async Task ImportPathsAsync(IReadOnlyList<string> paths)
    {
        IsBusy = true;
        ClearError();
        try
        {
            Directory.CreateDirectory(MediaStore);

            var skipped = 0;
            foreach (var sourcePath in paths)
            {
                var ext = Path.GetExtension(sourcePath);
                if (!AllowedExtensions.Contains(ext))
                {
                    _logger.LogWarning("Skipping '{FileName}' — unsupported extension '{Ext}'",
                        Path.GetFileName(sourcePath), ext);
                    skipped++;
                    continue;
                }

                var fileSize = new FileInfo(sourcePath).Length;
                if (fileSize > MaxMediaFileSizeBytes)
                {
                    _logger.LogWarning("Skipping '{FileName}' — size {SizeMb} MB exceeds limit",
                        Path.GetFileName(sourcePath), fileSize / 1_048_576);
                    skipped++;
                    continue;
                }

                var isVideo = VideoExtensions.Contains(ext);
                if (!MediaSignatureValidator.IsValid(sourcePath, isVideo))
                {
                    _logger.LogWarning("Skipping '{FileName}' — contents do not match a supported {Kind} format",
                        Path.GetFileName(sourcePath), isVideo ? "video" : "image");
                    skipped++;
                    continue;
                }

                var destPath  = GetUniqueDestinationPath(sourcePath);
                File.Copy(sourcePath, destPath);

                await _mediaService.AddAsync(new MediaFile
                {
                    FileName = Path.GetFileName(destPath),
                    FilePath = destPath,
                    Type     = isVideo ? MediaType.Video : MediaType.Image
                });
            }

            await LoadCoreAsync();

            if (skipped > 0)
                SetError(L("Media_Skipped", skipped));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import media");
            SetError(L("Media_ImportFailed"));
        }
        finally { IsBusy = false; }
    }

    // ── Delete ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteFileAsync(MediaFile file)
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            await _mediaService.DeleteAsync(file.Id);

            var resolvedPath = Path.GetFullPath(file.FilePath);
            var storeRoot    = Path.GetFullPath(MediaStore) + Path.DirectorySeparatorChar;
            if (resolvedPath.StartsWith(storeRoot, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(resolvedPath); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not remove media file from disk: {FileName}", file.FileName);
                }
            }
            else
            {
                _logger.LogWarning("Skipping disk delete — path outside media store: {FileName}", file.FileName);
            }

            if (SelectedFile?.Id == file.Id) SelectedFile = null;
            await LoadCoreAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete media file {Id}", file.Id);
            SetError(L("Media_DeleteFailed"));
        }
        finally { IsBusy = false; }
    }

    // ── Project ───────────────────────────────────────────────────────────

    [RelayCommand]
    private void ProjectFile(MediaFile file)
    {
        try
        {
            var slide = _mediaService.GenerateSlide(file);
            _projectionService.LoadSlides(new[] { slide }, file.FileName);
            SelectedFile = file;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to project media file {Id}", file.Id);
            SetError(L("Media_ProjectFailed"));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string GetUniqueDestinationPath(string sourcePath)
    {
        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(MediaStore, fileName);
        if (!File.Exists(destPath)) return destPath;

        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext  = Path.GetExtension(fileName);
        var n    = 1;
        do { destPath = Path.Combine(MediaStore, $"{name} ({n++}){ext}"); }
        while (File.Exists(destPath));
        return destPath;
    }
}
