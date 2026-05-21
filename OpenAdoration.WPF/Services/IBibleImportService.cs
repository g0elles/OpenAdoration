namespace OpenAdoration.WPF.Services;

public sealed record BibleImportCompletedArgs(string VersionName, int VerseCount);
public sealed record BibleImportFailedArgs(string Message, Exception? Exception);

public interface IBibleImportService
{
    bool   IsImporting   { get; }
    int    Progress      { get; }
    int    Total         { get; }
    string StatusMessage { get; }

    event EventHandler?                           StateChanged;
    event EventHandler<BibleImportCompletedArgs>? ImportCompleted;
    event EventHandler<BibleImportFailedArgs>?    ImportFailed;

    void StartImport(string filePath);
    void Cancel();
}
