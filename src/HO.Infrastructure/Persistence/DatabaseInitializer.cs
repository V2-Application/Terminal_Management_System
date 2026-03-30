using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Persistence;

/// <summary>
/// Handles DB creation and seeding robustly.
/// Works even if the DB exists but has no tables (handles failed migration edge case).
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, ILogger logger)
    {
        try
        {
            // Check if Stores table exists — if not, recreate from scratch
            var tablesExist = await TableExistsAsync(db, "Stores");

            if (!tablesExist)
            {
                logger.LogInformation("Tables not found — creating schema from EF model");

                // Drop any partial schema (safe in dev — removes __EFMigrationsHistory etc.)
                await db.Database.EnsureDeletedAsync();
                await db.Database.EnsureCreatedAsync();

                logger.LogInformation("Schema created — seeding sample data");
                await DatabaseSeeder.SeedAsync(db, logger);
            }
            else
            {
                logger.LogInformation("Database schema OK — checking seed data");
                await DatabaseSeeder.SeedAsync(db, logger);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Database initialization failed. " +
                "Check your connection string and ensure SQL Server is running. " +
                "Connection string: {Cs}",
                db.Database.GetConnectionString()?[..Math.Min(60, db.Database.GetConnectionString()?.Length ?? 0)]);
            throw; // Re-throw so startup shows a clear error
        }
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string tableName)
    {
        try
        {
            var conn = db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES " +
                "WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @name";

            var param = cmd.CreateParameter();
            param.ParameterName = "@name";
            param.Value = tableName;
            cmd.Parameters.Add(param);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch
        {
            return false; // DB doesn't exist at all
        }
        finally
        {
            // Don't close — EF manages connection lifecycle
        }
    }
}
