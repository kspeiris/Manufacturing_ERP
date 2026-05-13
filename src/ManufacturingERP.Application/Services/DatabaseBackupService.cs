using ManufacturingERP.Shared.Results;
using Microsoft.Data.Sqlite;

namespace ManufacturingERP.Application.Services;

public class DatabaseBackupService
{
    private readonly string _databasePath;

    public DatabaseBackupService(string databasePath)
    {
        _databasePath = databasePath;
    }

    public string DatabasePath => _databasePath;

    public async Task<Result<string>> CreateBackupAsync(string backupDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
            return Result<string>.Failure("Backup directory is required.");

        if (!File.Exists(_databasePath))
            return Result<string>.Failure("Database file was not found.");

        Directory.CreateDirectory(backupDirectory);
        var backupPath = Path.Combine(
            backupDirectory,
            $"manufacturing_erp_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db");

        await using var source = new SqliteConnection($"Data Source={_databasePath}");
        await using var destination = new SqliteConnection($"Data Source={backupPath}");
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);

        return Result<string>.Success(backupPath, $"Backup created: {backupPath}");
    }

    public async Task<Result> RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
            return Result.Failure("Backup path is required.");

        if (!File.Exists(backupPath))
            return Result.Failure("Backup file was not found.");

        var validation = await ValidateBackupAsync(backupPath, cancellationToken);
        if (!validation.IsSuccess)
            return validation;

        var databaseDirectory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
            Directory.CreateDirectory(databaseDirectory);

        SqliteConnection.ClearAllPools();
        File.Copy(backupPath, _databasePath, overwrite: true);
        DeleteRelatedSqliteFiles(_databasePath);

        return Result.Success("Database restored. Restart the application before continuing.");
    }

    public async Task<Result> ValidateBackupAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken);

            await using var integrity = connection.CreateCommand();
            integrity.CommandText = "PRAGMA quick_check;";
            var quickCheck = Convert.ToString(await integrity.ExecuteScalarAsync(cancellationToken));
            if (!string.Equals(quickCheck, "ok", StringComparison.OrdinalIgnoreCase))
                return Result.Failure("Backup integrity check failed.");

            await using var usersTable = connection.CreateCommand();
            usersTable.CommandText = """
                SELECT COUNT(1)
                FROM sqlite_master
                WHERE type = 'table' AND name = 'Users';
                """;
            var hasUsersTable = Convert.ToInt32(await usersTable.ExecuteScalarAsync(cancellationToken)) > 0;
            return hasUsersTable
                ? Result.Success("Backup is valid.")
                : Result.Failure("Backup does not look like a Manufacturing ERP database.");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Backup validation failed: {ex.Message}");
        }
    }

    private static void DeleteRelatedSqliteFiles(string databasePath)
    {
        foreach (var path in new[] { $"{databasePath}-shm", $"{databasePath}-wal" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
