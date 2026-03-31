using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, ILogger logger)
    {
        try
        {
            var tablesExist = await TableExistsAsync(db, "Stores");

            if (!tablesExist)
            {
                logger.LogInformation("Tables not found — creating schema from EF model");
                await db.Database.EnsureDeletedAsync();
                SqlConnection.ClearAllPools();
                await Task.Delay(500);
                await db.Database.CloseConnectionAsync();
                await db.Database.EnsureCreatedAsync();
                logger.LogInformation("Schema created — seeding data");
                await DatabaseSeeder.SeedAsync(db, logger);
                await SeedDefaultUsersAsync(db, logger);
            }
            else
            {
                // Ensure HoUsers table exists (added after initial deploy)
                var usersExist = await TableExistsAsync(db, "HoUsers");
                if (!usersExist)
                {
                    logger.LogInformation("HoUsers table missing — creating it");
                    await db.Database.ExecuteSqlRawAsync(@"
                        CREATE TABLE [dbo].[HoUsers] (
                            [UserId]             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
                            [Username]           NVARCHAR(50)     NOT NULL,
                            [FullName]           NVARCHAR(100)    NOT NULL,
                            [Email]              NVARCHAR(200)    NOT NULL,
                            [PasswordHash]       NVARCHAR(200)    NOT NULL,
                            [Role]               NVARCHAR(30)     NOT NULL DEFAULT 'HOOperator',
                            [IsActive]           BIT              NOT NULL DEFAULT 1,
                            [MustChangePassword] BIT              NOT NULL DEFAULT 0,
                            [LastLoginAt]        DATETIME2        NULL,
                            [LastLoginIp]        NVARCHAR(50)     NULL,
                            [FailedLoginCount]   INT              NOT NULL DEFAULT 0,
                            [LockedUntil]        DATETIME2        NULL,
                            [CreatedAt]          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                            [UpdatedAt]          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
                            [CreatedBy]          NVARCHAR(100)    NOT NULL DEFAULT 'SYSTEM',
                            CONSTRAINT UX_HoUsers_Username UNIQUE ([Username]),
                            CONSTRAINT UX_HoUsers_Email    UNIQUE ([Email])
                        )");
                    logger.LogInformation("HoUsers table created");
                }

                await DatabaseSeeder.SeedAsync(db, logger);
                await SeedDefaultUsersAsync(db, logger);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed. Check connection string.");
            throw;
        }
    }

    /// <summary>
    /// Seeds the 3 default user accounts if no users exist.
    /// Passwords are BCrypt hashed — never stored plain text.
    /// </summary>
    private static async Task SeedDefaultUsersAsync(AppDbContext db, ILogger logger)
    {
        if (await db.HoUsers.AnyAsync()) return;

        logger.LogInformation("Seeding default HO user accounts...");

        // Default accounts — change passwords immediately in production!
        var users = new[]
        {
            new HO.Domain.Entities.HoUser
            {
                Username     = "admin",
                FullName     = "Super Administrator",
                Email        = "admin@retailtms.in",
                PasswordHash = BCryptHelper.Hash("Admin@1234"),
                Role         = "SuperAdmin",
                IsActive     = true,
                CreatedBy    = "SEED"
            },
            new HO.Domain.Entities.HoUser
            {
                Username     = "hoadmin",
                FullName     = "HO Admin User",
                Email        = "hoadmin@retailtms.in",
                PasswordHash = BCryptHelper.Hash("HOAdmin@123"),
                Role         = "HOAdmin",
                IsActive     = true,
                CreatedBy    = "SEED"
            },
            new HO.Domain.Entities.HoUser
            {
                Username     = "operator",
                FullName     = "FY-Close Operator",
                Email        = "operator@retailtms.in",
                PasswordHash = BCryptHelper.Hash("Operator@123"),
                Role         = "HOOperator",
                IsActive     = true,
                CreatedBy    = "SEED"
            },
            new HO.Domain.Entities.HoUser
            {
                Username     = "viewer",
                FullName     = "Read-Only Viewer",
                Email        = "viewer@retailtms.in",
                PasswordHash = BCryptHelper.Hash("Viewer@123"),
                Role         = "Viewer",
                IsActive     = true,
                CreatedBy    = "SEED"
            },
        };

        db.HoUsers.AddRange(users);
        await db.SaveChangesAsync();

        logger.LogInformation("Default users seeded: admin, hoadmin, operator, viewer");
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
                "WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME=@name";
            var p = cmd.CreateParameter();
            p.ParameterName = "@name"; p.Value = tableName;
            cmd.Parameters.Add(p);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch { return false; }
    }
}

/// <summary>Simple BCrypt wrapper — works without the full NuGet package in Infrastructure</summary>
public static class BCryptHelper
{
    public static string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public static bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}
