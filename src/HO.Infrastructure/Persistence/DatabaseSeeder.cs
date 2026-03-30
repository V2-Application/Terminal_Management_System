using HO.Domain.Entities;
using HO.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Persistence;

/// <summary>
/// Seeds realistic sample data for development and demo purposes.
/// Called from Program.cs on startup when DB is empty.
/// </summary>
public static class DatabaseSeeder
{
    private static readonly string[] Regions = { "North", "South", "East", "West", "Central" };

    private static readonly (string region, string zone, string[] names)[] StoreData =
    {
        ("North",   "Delhi NCR",   new[]{"Connaught Place","Karol Bagh","Lajpat Nagar","Saket","Rohini","Dwarka","Janakpuri","Pitampura"}),
        ("North",   "Punjab",      new[]{"Ludhiana City","Amritsar Main","Chandigarh Sec17","Patiala","Jalandhar"}),
        ("South",   "Chennai",     new[]{"T Nagar","Anna Nagar","Adyar","Velachery","Tambaram","Porur"}),
        ("South",   "Bangalore",   new[]{"Koramangala","Indiranagar","Whitefield","Electronic City","HSR Layout"}),
        ("South",   "Hyderabad",   new[]{"Banjara Hills","Jubilee Hills","Hitech City","Kukatpally","Secunderabad"}),
        ("East",    "Kolkata",     new[]{"Park Street","Salt Lake","New Market","Behala","Dum Dum"}),
        ("East",    "Odisha",      new[]{"Bhubaneswar Main","Cuttack","Puri Rd","Berhampur"}),
        ("West",    "Mumbai",      new[]{"Andheri West","Bandra","Dadar","Malad","Thane","Borivali","Powai"}),
        ("West",    "Pune",        new[]{"FC Road","Kothrud","Aundh","Hadapsar","Pimpri"}),
        ("Central", "MP",          new[]{"Indore MG Rd","Bhopal DB Mall","Gwalior","Jabalpur","Ujjain"}),
    };

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Stores.AnyAsync())
        {
            logger.LogInformation("Database already seeded — skipping");
            return;
        }

        logger.LogInformation("Seeding database with sample retail store data...");

        var storeNum = 1;
        var rng      = new Random(42);

        foreach (var (region, zone, names) in StoreData)
        {
            foreach (var name in names)
            {
                var storeCode = "ST" + storeNum.ToString("D3");

                var store = new Store
                {
                    StoreCode     = storeCode,
                    StoreName     = name + " Store",
                    Region        = region,
                    Zone          = zone,
                    ContactEmail  = storeCode.ToLower() + "@retailtms.in",
                    ContactPhone  = "9" + rng.Next(100000000, 999999999).ToString(),
                    Priority      = storeNum <= 10 ? 1 : storeNum <= 30 ? 2 : 3,
                    Status        = StoreStatus.Active,
                    FYCloseStatus = FYCloseStatus.Pending,
                    CreatedBy     = "SEED",
                };

                db.Stores.Add(store);
                await db.SaveChangesAsync();

                // Add primary terminal for each store
                var terminal = new Terminal
                {
                    StoreId      = store.StoreId,
                    TerminalCode = storeCode + "-T01",
                    MachineId    = "BIOS-" + Guid.NewGuid().ToString("N")[..16].ToUpper(),
                    MachineName  = storeCode + "-POS01",
                    IpAddress    = "192.168." + (100 + storeNum / 256) + "." + (storeNum % 256),
                    OsVersion    = "Windows 10 Pro 22H2",
                    AgentVersion = "1.0.0",
                    PosVersion   = "AX2012 R3",
                    Status       = rng.Next(10) < 8 ? TerminalStatus.Active : TerminalStatus.Offline,
                    IsPrimary    = true,
                    LastHeartbeat = rng.Next(10) < 8
                        ? DateTime.UtcNow.AddMinutes(-rng.Next(1, 8))
                        : DateTime.UtcNow.AddHours(-rng.Next(2, 48)),
                    DiskFreeGB   = (decimal)(rng.NextDouble() * 200 + 20),
                };

                db.Terminals.Add(terminal);
                storeNum++;
            }
        }

        await db.SaveChangesAsync();

        // Seed one demo ScriptPackage so the FY-Close form works
        var pkg = new ScriptPackage
        {
            PackageName      = "FY2025-DLLs-v3",
            StepType         = "FY_CLOSE",
            Version          = "FY2025-v3",
            DllVersion       = "6.3.4000.1234",
            FileSize         = 18_432_000,
            Sha256Hash       = "a3f8c2d1e9b4f5a6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0",
            StoragePath      = @"C:\RetailTMS\Packages\FY2025-DLLs-v3.zip",
            IsActive         = true,
            FYYear           = "FY2025",
            UploadedBy       = "SEED",
        };
        db.ScriptPackages.Add(pkg);

        var rollbackPkg = new ScriptPackage
        {
            PackageName      = "FY2024-DLLs-v2 (Rollback)",
            StepType         = "FY_CLOSE",
            Version          = "FY2024-v2",
            DllVersion       = "6.3.3999.8765",
            FileSize         = 17_920_000,
            Sha256Hash       = "b4a9d3c2f1e0b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0",
            StoragePath      = @"C:\RetailTMS\Packages\FY2024-DLLs-v2.zip",
            IsActive         = false,
            IsRollbackPackage = true,
            FYYear           = "FY2025",
            UploadedBy       = "SEED",
        };
        db.ScriptPackages.Add(rollbackPkg);

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Seeding complete — {Stores} stores, {Terminals} terminals, 2 packages",
            storeNum - 1, storeNum - 1);
    }
}
