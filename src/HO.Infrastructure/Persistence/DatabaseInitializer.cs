using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Persistence;

/// <summary>
/// Handles DB creation and seeding robustly.
/// Clears the connection pool between EnsureDeleted and EnsureCreated
/// so stale connections don't cause 'connection is broken' errors.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, ILogger logger)
    {
        var connString = db.Database.GetConnectionString()!;

        // ── Step 1: Check if tables already exist ────────────────────────────
        bool tablesExist = false;
        try
        {
            // Just querying the table count is enough — will throw if table missing
            tablesExist = await db.Stores.AnyAsync() || true;
            logger.LogInformation("Database tables found — checking seed data");
        }
        catch (SqlException ex) when (
            ex.Message.Contains("Invalid object name") ||
            ex.Message.Contains("Cannot open database") ||
            ex.Number == 208  /* Invalid object name */ ||
            ex.Number == 4060 /* Cannot open database */)
        {
            tablesExist = false;
            logger.LogInformation("Tables missing (SqlException {N}) — will create schema", ex.Number);
        }
        catch (Exception ex)
        {
            tablesExist = false;
            logger.LogWarning(ex, "Could not check tables — will attempt schema creation");
        }

        // ── Step 2: Create schema if needed ──────────────────────────────────
        if (!tablesExist)
        {
            logger.LogInformation("Creating database schema from EF model...");

            try
            {
                // First: drop existing (may be empty from a previous failed attempt)
                await db.Database.EnsureDeletedAsync();

                // CRITICAL: clear connection pool so EnsureCreated gets a fresh connection
                SqlConnection.ClearAllPools();
                await Task.Delay(500); // brief pause for pool to drain

                // Dispose and re-open so EF also gets a fresh connection
                await db.Database.CloseConnectionAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "EnsureDeleted warning (ignorable if DB did not exist)");
            }

            // Now create all tables from the EF model
            await db.Database.EnsureCreatedAsync();
            logger.LogInformation("Schema created successfully");

            // ── Step 3: Seed sample data ──────────────────────────────────
            logger.LogInformation("Seeding sample data...");
            await DatabaseSeeder.SeedAsync(db, logger);
            return;
        }

        // ── Tables exist — just check seed ────────────────────────────────────
        await DatabaseSeeder.SeedAsync(db, logger);
    }
}
