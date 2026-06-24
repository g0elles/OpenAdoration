using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Domain.Enums;
using OpenAdoration.WPF.Helpers;

namespace OpenAdoration.WPF.ViewModels;

public partial class MediaViewModel : BaseViewModel
{
    private readonly IMediaService       _mediaService;
    private readonly IThemeService       _themeService;
    private readonly IProjectionService  _projectionService;
    private readonly IAppSettingsService _appSettings;
    private readonly AppPaths            _appPaths;
    private readonly ILogger<MediaViewModel> _logger;

    // Canonical store path (honours OA_DATA_DIR via AppPaths, unlike a hardcoded LocalAppData path).
    private string MediaStore => _appPaths.MediaDirectory;

    private const long MaxMediaFileSizeBytes = 1L * 1024 * 1024 * 1024; // 1 GB

    [ObservableProperty] private ObservableCollection<MediaFile> _mediaFiles = new();
    [ObservableProperty] private ObservableCollection<MediaFile> _backgrounds = new();
    [ObservableProperty] private MediaFile? _selectedFile;

    // Backgrounds are an exclusive subsection: the toggle swaps which list (and import target) is active.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayedFiles))]
    [NotifyPropertyChangedFor(nameof(HasDisplayed))]
    [NotifyPropertyChangedFor(nameof(IsMediaTab))]
    private bool _showBackgrounds;

    public bool IsMediaTab => !ShowBackgrounds;
    public ObservableCollection<MediaFile> DisplayedFiles => ShowBackgrounds ? Backgrounds : MediaFiles;
    public bool HasDisplayed => DisplayedFiles.Count > 0;

    public MediaViewModel(
        IMediaService       mediaService,
        IThemeService       themeService,
        IProjectionService  projectionService,
        IAppSettingsService appSettings,
        AppPaths            appPaths,
        ILogger<MediaViewModel> logger)
    {
        _mediaService      = mediaService;
        _themeService      = themeService;
        _projectionService = projectionService;
        _appSettings       = appSettings;
        _appPaths          = appPaths;
        _logger            = logger;
    }

    [RelayCommand]
    private void ShowMediaTab() => ShowBackgrounds = false;

    [RelayCommand]
    private void ShowBackgroundsTab() => ShowBackgrounds = true;

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

        var backgrounds = await _mediaService.GetBackgroundsAsync();
        Backgrounds.Clear();
        foreach (var b in backgrounds) Backgrounds.Add(b);

        OnPropertyChanged(nameof(DisplayedFiles));
        OnPropertyChanged(nameof(HasDisplayed));
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
                if (!MediaFormats.IsSupported(sourcePath))
                {
                    _logger.LogWarning("Skipping '{FileName}' — unsupported extension '{Ext}'",
                        Path.GetFileName(sourcePath), Path.GetExtension(sourcePath));
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

                var isVideo = MediaFormats.IsVideo(sourcePath);
                if (!MediaSignatureValidator.IsValid(sourcePath, isVideo))
                {
                    _logger.LogWarning("Skipping '{FileName}' — contents do not match a supported {Kind} format",
                        Path.GetFileName(sourcePath), isVideo ? "video" : "image");
                    skipped++;
                    continue;
                }

                // Dedup by content within the active category: the same bytes already there reuse
                // that record (no copy). Background and general media dedup independently.
                var hash = ComputeHash(sourcePath);
                if (await _mediaService.GetByContentHashAsync(hash, isBackground: ShowBackgrounds) is not null)
                {
                    _logger.LogInformation("Skipping '{FileName}' — already in the library (same content)",
                        Path.GetFileName(sourcePath));
                    skipped++;
                    continue;
                }

                var destPath  = GetUniqueDestinationPath(sourcePath);
                File.Copy(sourcePath, destPath);

                await _mediaService.AddAsync(new MediaFile
                {
                    FileName     = Path.GetFileName(destPath),
                    FilePath     = destPath,
                    Type         = isVideo ? MediaType.Video : MediaType.Image,
                    ContentHash  = hash,
                    IsBackground = ShowBackgrounds
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
            // Delete-guard: a background still referenced by themes must not be removed out from
            // under them. Block and tell the operator to detach it in the theme(s) first.
            if (file.IsBackground)
            {
                var inUse = (await _themeService.GetAllAsync())
                    .Count(t => PathEquals(t.BackgroundImagePath, file.FilePath)
                             || PathEquals(t.BackgroundVideoPath, file.FilePath));
                if (inUse > 0)
                {
                    SetError(L("Media_BgInUse", inUse));
                    return;
                }
            }

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
            var slide = _mediaService.GenerateSlide(file, ThemeCascade.ForMedia(null, _appSettings.Current));
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

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
    }

    private static bool PathEquals(string? a, string? b) =>
        !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private string GetUniqueDestinationPath(string sourcePath)
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
