using Microsoft.Data.Sqlite;

namespace OpenAdoration.Infrastructure.Backup;

public static class SqliteSnapshot
{
    /// <summary>
    /// Writes a consistent copy of the SQLite database at <paramref name="dbPath"/> to
    /// <paramref name="destinationPath"/> via the online backup API — safe while the app holds
    /// the DB open, and handles WAL. <c>Pooling=False</c> is essential: the default pool keeps
    /// the file handle open after the connection is disposed, which would lock the snapshot so
    /// the caller couldn't move or delete it.
    /// </summary>
    public static void Create(string dbPath, string destinationPath)
    {
        using var source      = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        using var destination = new SqliteConnection($"Data Source={destinationPath};Pooling=False");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }
}
