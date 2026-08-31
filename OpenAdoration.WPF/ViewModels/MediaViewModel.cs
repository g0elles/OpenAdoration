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
using OpenAdoration.WPF.Services;

namespace OpenAdoration.WPF.ViewModels;

public partial class MediaViewModel : BaseViewModel
{
    private readonly IMediaService       _mediaService;
    private readonly IThemeService       _themeService;
    private readonly IProjectionService  _projectionService;
    private readonly IAppSettingsService _appSettings;
    private readonly IStageNavigationService _stageNavigation;
    private readonly AppPaths            _appPaths;
    private readonly ILogger<MediaViewModel> _logger;

    // Canonical store path (honours OA_DATA_DIR via AppPaths, unlike a hardcoded LocalAppData path).
    private string MediaStore => _appPaths.MediaDirectory;

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
        IStageNavigationService stageNavigation,
        AppPaths            appPaths,
        ILogger<MediaViewModel> logger)
    {
        _mediaService      = mediaService;
        _themeService      = themeService;
        _projectionService = projectionService;
        _appSettings       = appSettings;
        _stageNavigation   = stageNavigation;
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

                // Size check + signature validation + hashing read the whole file; keep them off the
                // UI thread so importing big/many videos doesn't freeze the window (P1).
                var prepared = await Task.Run(() => ValidateForImport(sourcePath));
                if (prepared is not { } prep) { skipped++; continue; }
                var (hash, isVideo) = prep;

                // Dedup by content within the active category: the same bytes already there reuse
                // that record (no copy). Background and general media dedup independently.
                if (await _mediaService.GetByContentHashAsync(hash, isBackground: ShowBackgrounds) is not null)
                {
                    _logger.LogInformation("Skipping '{FileName}' — already in the library (same content)",
                        Path.GetFileName(sourcePath));
                    skipped++;
                    continue;
                }

                var destPath  = GetUniqueDestinationPath(sourcePath);
                await Task.Run(() => File.Copy(sourcePath, destPath));

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
            UpdateNextMediaPreview(file);
            SelectedFile = file;
            _stageNavigation.NavigateToStage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to project media file {Id}", file.Id);
            SetError(L("Media_ProjectFailed"));
        }
    }

    // Standalone (non-service) projection has no natural "next item" — mirror it via the same
    // cross-item preview hint ServiceScheduleViewModel feeds Stage View, so its "up next" pane
    // works for standalone media too. Never touch the hint while a real service owns it.
    private void UpdateNextMediaPreview(MediaFile current)
    {
        if (_projectionService.IsServiceScheduleActive) return;

        var next = FindNextMediaFile(DisplayedFiles, current);
        if (next is null)
        {
            _projectionService.SetNextScheduleItemPreview(null);
            _projectionService.SetStandaloneNextItem(null, null);
            return;
        }

        var nextSlide = _mediaService.GenerateSlide(next, ThemeCascade.ForMedia(null, _appSettings.Current));
        _projectionService.SetNextScheduleItemPreview(nextSlide);
        _projectionService.SetStandaloneNextItem(new[] { nextSlide }, next.FileName);
    }

    /// <summary>Pure list-index lookup: the file after <paramref name="current"/> in <paramref name="files"/>,
    /// matched by Id. Null when <paramref name="current"/> is last or not present. Unit-testable without DI
    /// (mirrors StageViewModel.ComputeMirrorScale/BuildSlideListItems).</summary>
    public static MediaFile? FindNextMediaFile(IReadOnlyList<MediaFile> files, MediaFile current)
    {
        for (var i = 0; i < files.Count; i++)
        {
            if (files[i].Id == current.Id) return i + 1 < files.Count ? files[i + 1] : null;
        }
        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    // Runs off the UI thread (Task.Run). Returns null + logs the reason when a file is rejected.
    private (string Hash, bool IsVideo)? ValidateForImport(string sourcePath)
    {
        var fileSize = new FileInfo(sourcePath).Length;
        if (fileSize > MediaFormats.MaxFileSizeBytes)
        {
            _logger.LogWarning("Skipping '{FileName}' — size {SizeMb} MB exceeds limit",
                Path.GetFileName(sourcePath), fileSize / 1_048_576);
            return null;
        }

        var isVideo = MediaFormats.IsVideo(sourcePath);
        if (!MediaSignatureValidator.IsValid(sourcePath, isVideo))
        {
            _logger.LogWarning("Skipping '{FileName}' — contents do not match a supported {Kind} format",
                Path.GetFileName(sourcePath), isVideo ? "video" : "image");
            return null;
        }

        return (ComputeHash(sourcePath), isVideo);
    }

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
