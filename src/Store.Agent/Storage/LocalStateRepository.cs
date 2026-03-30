using Microsoft.Data.Sqlite;
using Store.Agent.Execution;
using Store.Agent.Models;

namespace Store.Agent.Services;

/// <summary>
/// SQLite-backed local state for the agent.
/// Provides idempotency (nonce cache) and offline result caching.
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
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ExecutionRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CommandId TEXT NOT NULL,
                CommandNonce TEXT NOT NULL UNIQUE,
                CommandType TEXT NOT NULL,
                Status TEXT NOT NULL DEFAULT 'PENDING',
                ExitCode INTEGER,
                Stdout TEXT,
                Stderr TEXT,
                DurationMs INTEGER,
                ResultSubmitted INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                CompletedAt TEXT
            );
            CREATE INDEX IF NOT EXISTS IX_Nonce ON ExecutionRecords(CommandNonce);
            CREATE INDEX IF NOT EXISTS IX_Submitted ON ExecutionRecords(ResultSubmitted, Status);
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task<bool> IsAlreadyExecutedAsync(Guid nonce)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM ExecutionRecords WHERE CommandNonce = $nonce";
        cmd.Parameters.AddWithValue("$nonce", nonce.ToString());
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }

    public async Task RecordExecutionAsync(Guid commandId, Guid nonce, string commandType, ExecutionResult result)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO ExecutionRecords
            (CommandId, CommandNonce, CommandType, Status, ExitCode, Stdout, Stderr, DurationMs, CreatedAt, CompletedAt)
            VALUES ($cmdId, $nonce, $type, $status, $exitCode, $stdout, $stderr, $duration, $created, $completed)
            """;
        cmd.Parameters.AddWithValue("$cmdId", commandId.ToString());
        cmd.Parameters.AddWithValue("$nonce", nonce.ToString());
        cmd.Parameters.AddWithValue("$type", commandType);
        cmd.Parameters.AddWithValue("$status", result.ExitCode == 0 ? "SUCCESS" : "FAILED");
        cmd.Parameters.AddWithValue("$exitCode", result.ExitCode);
        cmd.Parameters.AddWithValue("$stdout", result.Stdout ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$stderr", result.Stderr ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$duration", result.DurationMs);
        cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$completed", DateTime.UtcNow.ToString("O"));
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
