using System.IO;
using System.Xml;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAdoration.Application.Services;
using OpenAdoration.WPF.Helpers.BibleImport;

namespace OpenAdoration.WPF.Services;

/// <summary>
/// Singleton service that runs Bible imports on a thread-pool thread so the UI
/// remains fully responsive. Survives navigation away from BibleView — a transient
/// BibleViewModel subscribes to events and shows progress while visible.
/// </summary>
public sealed class BibleImportService : IBibleImportService
{
    private readonly IServiceScopeFactory        _scopeFactory;
    private readonly ILogger<BibleImportService> _logger;

    private CancellationTokenSource? _cts;

    public bool   IsImporting   { get; private set; }
    public int    Progress      { get; private set; }
    public int    Total         { get; private set; }
    public string StatusMessage { get; private set; } = string.Empty;

    public event EventHandler?                           StateChanged;
    public event EventHandler<BibleImportCompletedArgs>? ImportCompleted;
    public event EventHandler<BibleImportFailedArgs>?    ImportFailed;

    public BibleImportService(IServiceScopeFactory scopeFactory, ILogger<BibleImportService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    public void StartImport(string filePath)
    {
        if (IsImporting) return;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsImporting   = true;
        Progress      = 0;
        Total         = 0;
        StatusMessage = "Detecting format...";
        RaiseStateChanged();

        // Progress<int> must be created on the UI thread so it captures the UI
        // SynchronizationContext and automatically marshals Report() callbacks back to it.
        var progress = new Progress<int>(count =>
        {
            Progress      = count;
            StatusMessage = $"Importing {count:N0} / {Total:N0} verses...";
            RaiseStateChanged();
        });

        _ = Task.Run(() => RunImportAsync(filePath, progress, ct), ct);
    }

    public void Cancel() => _cts?.Cancel();

    private async Task RunImportAsync(string filePath, IProgress<int> progress, CancellationToken ct)
    {
        try
        {
            NotifyOnUiThread("Parsing file...");

            // Synchronous parse runs on the thread-pool thread (caller is Task.Run).
            var result = BibleFormatDispatcher.Import(filePath);
            ct.ThrowIfCancellationRequested();

            var (version, books, verses) = result;
            int total = verses.Count;

            InvokeOnUi(() =>
            {
                Total         = total;
                StatusMessage = $"Importing 0 / {total:N0} verses...";
                RaiseStateChanged();
            });

            await using var scope       = _scopeFactory.CreateAsyncScope();
            var             bibleService = scope.ServiceProvider.GetRequiredService<IBibleService>();

            await bibleService.ImportVersionAsync(version, books, verses, progress, ct);

            var completed = new BibleImportCompletedArgs(version.Name, verses.Count);
            InvokeOnUi(() =>
            {
                IsImporting   = false;
                Progress      = 0;
                Total         = 0;
                StatusMessage = string.Empty;
                RaiseStateChanged();
                ImportCompleted?.Invoke(this, completed);
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Bible import cancelled");
            ResetOnUiThread();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bible import failed");
            var failed = new BibleImportFailedArgs(GetUserMessage(ex), ex);
            InvokeOnUi(() =>
            {
                IsImporting   = false;
                Progress      = 0;
                Total         = 0;
                StatusMessage = string.Empty;
                RaiseStateChanged();
                ImportFailed?.Invoke(this, failed);
            });
        }
    }

    private static void InvokeOnUi(Action action) =>
        System.Windows.Application.Current.Dispatcher.BeginInvoke(action);

    private void NotifyOnUiThread(string message) =>
        InvokeOnUi(() => { StatusMessage = message; RaiseStateChanged(); });

    private void ResetOnUiThread() =>
        InvokeOnUi(() =>
        {
            IsImporting   = false;
            Progress      = 0;
            Total         = 0;
            StatusMessage = string.Empty;
            RaiseStateChanged();
        });

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private static string GetUserMessage(Exception ex) => ex switch
    {
        FileNotFoundException     => "The selected file could not be opened.",
        InvalidOperationException => ex.Message,
        XmlException              => "The file could not be read as XML. It may be corrupted or in an unsupported encoding.",
        JsonException             => "The file could not be read as JSON. It may be corrupted or in an unsupported format.",
        InvalidDataException      => $"Invalid file format: {ex.Message}",
        _                         => "Import failed. Check the log for details."
    };
}
