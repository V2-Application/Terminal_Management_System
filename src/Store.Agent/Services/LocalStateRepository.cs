using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Store.Agent.Execution;
using Store.Agent.Models;

namespace Store.Agent.Services;  // intentionally in Services namespace so Worker.cs can resolve it

/// <summary>
/// SQLite-backed local state repository for the store agent.
/// Provides:
/// - Command nonce cache (idempotency — prevents duplicate execution)
/// - Offline result caching (results submitted to HO on reconnect)
/// </summary>
public class LocalStateRepository
{
    private readonly string _dbPath;
    private readonly ILogger<LocalStateRepository> _logger;

    public LocalStateRepository(AgentConfig config, ILogger<LocalStateRepository> logger)
    {
        _dbPath = config.LocalDbPath;
        _logger = logger;
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ExecutionRecords (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                CommandId       TEXT    NOT NULL,
                CommandNonce    TEXT    NOT NULL,
                CommandType     TEXT    NOT NULL,
                Status          TEXT    NOT NULL DEFAULT 'PENDING',
                ExitCode        INTEGER,
                Stdout          TEXT,
                Stderr          TEXT,
                DurationMs      INTEGER,
                ResultSubmitted INTEGER NOT NULL DEFAULT 0,
                CreatedAt       TEXT    NOT NULL,
                CompletedAt     TEXT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS UX_Nonce
                ON ExecutionRecords(CommandNonce);
            CREATE INDEX IF NOT EXISTS IX_Pending
                ON ExecutionRecords(ResultSubmitted, Status);";
        cmd.ExecuteNonQuery();
        _logger.LogDebug("Local SQLite schema verified at {DbPath}", _dbPath);
    }

    /// <summary>Check if a command nonce was already executed (idempotency guard).</summary>
    public async Task<bool> IsAlreadyExecutedAsync(Guid nonce)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM ExecutionRecords WHERE CommandNonce = $nonce";
        cmd.Parameters.AddWithValue("$nonce", nonce.ToString());
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }

    /// <summary>Record a completed execution for idempotency and offline caching.</summary>
    public async Task RecordExecutionAsync(
        Guid commandId, Guid nonce, string commandType, ExecutionResult result)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO ExecutionRecords
                (CommandId, CommandNonce, CommandType, Status, ExitCode,
                 Stdout, Stderr, DurationMs, CreatedAt, CompletedAt)
            VALUES
                ($cmdId, $nonce, $type, $status, $exitCode,
                 $stdout, $stderr, $duration, $created, $completed)";

        cmd.Parameters.AddWithValue("$cmdId",    commandId.ToString());
        cmd.Parameters.AddWithValue("$nonce",    nonce.ToString());
        cmd.Parameters.AddWithValue("$type",     commandType);
        cmd.Parameters.AddWithValue("$status",   result.ExitCode == 0 ? "SUCCESS" : "FAILED");
        cmd.Parameters.AddWithValue("$exitCode", result.ExitCode);
        cmd.Parameters.AddWithValue("$stdout",   (object?)result.Stdout  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$stderr",   (object?)result.Stderr  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$duration", result.DurationMs);
        cmd.Parameters.AddWithValue("$created",  DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$completed",DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Get unsubmitted results for offline replay on reconnect.</summary>
    public async Task<IEnumerable<(Guid CommandId, int ExitCode, string? Stdout, string? Stderr, long DurationMs)>>
        GetPendingResultsAsync()
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT CommandId, ExitCode, Stdout, Stderr, DurationMs
            FROM   ExecutionRecords
            WHERE  ResultSubmitted = 0 AND Status IN ('SUCCESS','FAILED')
            LIMIT  50";

        var results = new List<(Guid, int, string?, string?, long)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add((
                Guid.Parse(reader.GetString(0)),
                reader.IsDBNull(1) ? -1 : reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? 0   : reader.GetInt64(4)));
        }
        return results;
    }

    public async Task MarkResultSubmittedAsync(Guid commandId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE ExecutionRecords SET ResultSubmitted = 1 WHERE CommandId = $id";
        cmd.Parameters.AddWithValue("$id", commandId.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        return conn;
    }
}
