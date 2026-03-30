using HO.Domain.Entities;
using HO.Domain.Enums;
using HO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HO.Web.Controllers;

/// <summary>
/// Full CRUD admin panel for all RetailTMS entities.
/// All endpoints return JSON for AJAX consumption.
/// </summary>
[Authorize]
[Route("Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    public AdminController(AppDbContext db) => _db = db;

    // ── Index — main admin panel ────────────────────────────────────────────
    [HttpGet("")]
    public IActionResult Index() => View();

    // ══════════════════════════════════════════════════════════════════════
    // STORES CRUD
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("Stores")]
    public async Task<JsonResult> GetStores(
        string? region, string? status, string? search,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var q = _db.Stores.Include(s => s.Terminals).AsQueryable();
        if (!string.IsNullOrEmpty(region)) q = q.Where(s => s.Region == region);
        if (!string.IsNullOrEmpty(status)) q = q.Where(s => s.Status.ToString() == status);
        if (!string.IsNullOrEmpty(search))
            q = q.Where(s => s.StoreCode.Contains(search) || s.StoreName.Contains(search));

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(s => s.Region).ThenBy(s => s.StoreCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new {
                s.StoreId, s.StoreCode, s.StoreName, s.Region, s.Zone,
                s.Priority, s.Status, s.FYCloseStatus, s.ContactEmail,
                s.ContactPhone, s.IsDeleted, s.CreatedAt, s.UpdatedAt,
                TerminalCount = s.Terminals.Count,
                ActiveTerminals = s.Terminals.Count(t => t.Status == TerminalStatus.Active)
            }).ToListAsync(ct);

        return Json(new { total, page, pageSize, items });
    }

    [HttpGet("Stores/{id}")]
    public async Task<JsonResult> GetStore(Guid id, CancellationToken ct)
    {
        var s = await _db.Stores.Include(x => x.Terminals).FirstOrDefaultAsync(x => x.StoreId == id, ct);
        if (s == null) return Json(new { error = "Not found" });
        return Json(s);
    }

    [HttpPost("Stores")]
    public async Task<JsonResult> CreateStore([FromBody] StoreUpsertDto dto, CancellationToken ct)
    {
        if (await _db.Stores.AnyAsync(s => s.StoreCode == dto.StoreCode, ct))
            return Json(new { success = false, error = "Store code already exists" });

        var store = new Store {
            StoreCode = dto.StoreCode, StoreName = dto.StoreName,
            Region = dto.Region, Zone = dto.Zone, Priority = dto.Priority,
            ContactEmail = dto.ContactEmail, ContactPhone = dto.ContactPhone,
            Status = Enum.Parse<StoreStatus>(dto.Status), CreatedBy = User.Identity?.Name ?? "ADMIN"
        };
        _db.Stores.Add(store);
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true, storeId = store.StoreId });
    }

    [HttpPut("Stores/{id}")]
    public async Task<JsonResult> UpdateStore(Guid id, [FromBody] StoreUpsertDto dto, CancellationToken ct)
    {
        var store = await _db.Stores.FindAsync(new object[]{id}, ct);
        if (store == null) return Json(new { success = false, error = "Not found" });

        store.StoreName = dto.StoreName; store.Region = dto.Region;
        store.Zone = dto.Zone; store.Priority = dto.Priority;
        store.ContactEmail = dto.ContactEmail; store.ContactPhone = dto.ContactPhone;
        store.Status = Enum.Parse<StoreStatus>(dto.Status);
        store.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true });
    }

    [HttpPatch("Stores/{id}/status")]
    public async Task<JsonResult> SetStoreStatus(Guid id, [FromBody] StatusDto dto, CancellationToken ct)
    {
        var store = await _db.Stores.FindAsync(new object[]{id}, ct);
        if (store == null) return Json(new { success = false, error = "Not found" });
        store.Status = Enum.Parse<StoreStatus>(dto.Status);
        store.FYCloseStatus = dto.FYCloseStatus != null ? Enum.Parse<FYCloseStatus>(dto.FYCloseStatus) : store.FYCloseStatus;
        store.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true });
    }

    [HttpDelete("Stores/{id}")]
    public async Task<JsonResult> DeleteStore(Guid id, CancellationToken ct)
    {
        var store = await _db.Stores.FindAsync(new object[]{id}, ct);
        if (store == null) return Json(new { success = false, error = "Not found" });
        store.IsDeleted = true; store.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true });
    }

    // ══════════════════════════════════════════════════════════════════════
    // TERMINALS CRUD
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("Terminals")]
    public async Task<JsonResult> GetTerminals(
        Guid? storeId, string? status, string? search,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var q = _db.Terminals.Include(t => t.Store).AsQueryable();
        if (storeId.HasValue) q = q.Where(t => t.StoreId == storeId.Value);
        if (!string.IsNullOrEmpty(status)) q = q.Where(t => t.Status.ToString() == status);
        if (!string.IsNullOrEmpty(search))
            q = q.Where(t => t.TerminalCode.Contains(search) || t.MachineName.Contains(search));

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(t => t.Store.StoreCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new {
                t.TerminalId, t.StoreId,
                StoreCode = t.Store.StoreCode, StoreName = t.Store.StoreName,
                t.TerminalCode, t.MachineName, t.IpAddress, t.OsVersion,
                t.AgentVersion, t.PosVersion, t.Status, t.IsPrimary,
                t.LastHeartbeat, t.DiskFreeGB, t.IsDeleted, t.CreatedAt, t.UpdatedAt
            }).ToListAsync(ct);

        return Json(new { total, page, pageSize, items });
    }

    [HttpPut("Terminals/{id}")]
    public async Task<JsonResult> UpdateTerminal(Guid id, [FromBody] TerminalUpdateDto dto, CancellationToken ct)
    {
        var t = await _db.Terminals.FindAsync(new object[]{id}, ct);
        if (t == null) return Json(new { success = false, error = "Not found" });
        t.IpAddress = dto.IpAddress; t.AgentVersion = dto.AgentVersion;
        t.PosVersion = dto.PosVersion; t.IsPrimary = dto.IsPrimary;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true });
    }

    [HttpPatch("Terminals/{id}/status")]
    public async Task<JsonResult> SetTerminalStatus(Guid id, [FromBody] StatusDto dto, CancellationToken ct)
    {
        var t = await _db.Terminals.FindAsync(new object[]{id}, ct);
        if (t == null) return Json(new { success = false, error = "Not found" });
        t.Status = Enum.Parse<TerminalStatus>(dto.Status);
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true });
    }

    [HttpDelete("Terminals/{id}")]
    public async Task<JsonResult> DeleteTerminal(Guid id, CancellationToken ct)
    {
        var t = await _db.Terminals.FindAsync(new object[]{id}, ct);
        if (t == null) return Json(new { success = false, error = "Not found" });
        t.IsDeleted = true; t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true });
    }

    // ══════════════════════════════════════════════════════════════════════
    // COMMANDS CRUD
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("Commands")]
    public async Task<JsonResult> GetCommands(
        string? status, string? commandType, Guid? storeId,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var q = _db.Commands
            .Include(c => c.Terminal).ThenInclude(t => t.Store)
            .Include(c => c.Executions).AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(c => c.Status.ToString() == status);
        if (!string.IsNullOrEmpty(commandType)) q = q.Where(c => c.CommandType.ToString() == commandType);
        if (storeId.HasValue) q = q.Where(c => c.StoreId == storeId.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new {
                c.CommandId, c.CommandType, c.Status, c.Priority,
                c.StoreId, StoreCode = c.Terminal.Store.StoreCode,
                c.TerminalId, TerminalCode = c.Terminal.TerminalCode,
                c.FYJobId, c.PackageId,
                c.CreatedAt, c.DispatchedAt, c.StartedAt, c.CompletedAt,
                c.RetryCount, c.MaxRetries, c.RetryAfter,
                LastExitCode = c.Executions.OrderByDescending(e=>e.StartedAt)
                    .Select(e=>e.ExitCode).FirstOrDefault()
            }).ToListAsync(ct);

        return Json(new { total, page, pageSize, items });
    }

    [HttpPatch("Commands/{id}/status")]
    public async Task<JsonResult> SetCommandStatus(Guid id, [FromBody] StatusDto dto, CancellationToken ct)
    {
        var cmd = await _db.Commands.FindAsync(new object[]{id}, ct);
        if (cmd == null) return Json(new { success = false, error = "Not found" });
        cmd.Status = Enum.Parse<CommandStatus>(dto.Status);
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true });
    }

    [HttpDelete("Commands/{id}")]
    public async Task<JsonResult> CancelCommand(Guid id, CancellationToken ct)
    {
        var cmd = await _db.Commands.FindAsync(new object[]{id}, ct);
        if (cmd == null) return Json(new { success = false, error = "Not found" });
        if (cmd.Status == CommandStatus.Queued || cmd.Status == CommandStatus.Dispatched)
            cmd.Status = CommandStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true, message = "Command cancelled" });
    }

    // ══════════════════════════════════════════════════════════════════════
    // FY JOBS CRUD
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("FYJobs")]
    public async Task<JsonResult> GetFYJobs(CancellationToken ct)
    {
        var items = await _db.FinancialYearJobs
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new {
                j.FYJobId, j.FYYear, j.Status, j.Phase,
                j.WaveSize, j.WaveIntervalMinutes,
                j.TotalStores, j.CompletedStores, j.FailedStores, j.OfflineStores,
                j.ScriptPackageId, j.RollbackPackageId,
                j.ExecutionWindowStart, j.ExecutionWindowEnd,
                j.StartedAt, j.CompletedAt, j.StartedBy,
                CompletionPct = j.TotalStores > 0
                    ? (int)(j.CompletedStores * 100.0 / j.TotalStores) : 0
            }).ToListAsync(ct);
        return Json(items);
    }

    [HttpPatch("FYJobs/{id}/status")]
    public async Task<JsonResult> SetFYJobStatus(Guid id, [FromBody] StatusDto dto, CancellationToken ct)
    {
        var job = await _db.FinancialYearJobs.FindAsync(new object[]{id}, ct);
        if (job == null) return Json(new { success = false, error = "Not found" });
        job.Status = Enum.Parse<FYJobStatus>(dto.Status);
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true });
    }

    [HttpPut("FYJobs/{id}")]
    public async Task<JsonResult> UpdateFYJob(Guid id, [FromBody] FYJobUpdateDto dto, CancellationToken ct)
    {
        var job = await _db.FinancialYearJobs.FindAsync(new object[]{id}, ct);
        if (job == null) return Json(new { success = false, error = "Not found" });
        job.WaveSize = dto.WaveSize;
        job.WaveIntervalMinutes = dto.WaveIntervalMinutes;
        job.ExecutionWindowStart = dto.ExecutionWindowStart;
        job.ExecutionWindowEnd = dto.ExecutionWindowEnd;
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true });
    }

    // ══════════════════════════════════════════════════════════════════════
    // SCRIPT PACKAGES CRUD
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("Packages")]
    public async Task<JsonResult> GetPackages(CancellationToken ct)
    {
        var items = await _db.ScriptPackages
            .OrderByDescending(p => p.UploadedAt)
            .Select(p => new {
                p.PackageId, p.PackageName, p.StepType, p.Version, p.DllVersion,
                p.FileSize, p.Sha256Hash, p.StoragePath, p.IsActive,
                p.IsRollbackPackage, p.FYYear, p.UploadedAt, p.UploadedBy, p.IsDeleted
            }).ToListAsync(ct);
        return Json(items);
    }

    [HttpPost("Packages")]
    public async Task<JsonResult> CreatePackage([FromBody] PackageCreateDto dto, CancellationToken ct)
    {
        var pkg = new ScriptPackage {
            PackageName = dto.PackageName, StepType = dto.StepType,
            Version = dto.Version, DllVersion = dto.DllVersion,
            FileSize = dto.FileSize, Sha256Hash = dto.Sha256Hash,
            StoragePath = dto.StoragePath, FYYear = dto.FYYear,
            IsActive = dto.IsActive, IsRollbackPackage = dto.IsRollbackPackage,
            UploadedBy = User.Identity?.Name ?? "ADMIN"
        };
        _db.ScriptPackages.Add(pkg);
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true, packageId = pkg.PackageId });
    }

    [HttpPut("Packages/{id}")]
    public async Task<JsonResult> UpdatePackage(Guid id, [FromBody] PackageCreateDto dto, CancellationToken ct)
    {
        var pkg = await _db.ScriptPackages.FindAsync(new object[]{id}, ct);
        if (pkg == null) return Json(new { success = false, error = "Not found" });
        pkg.PackageName = dto.PackageName; pkg.Version = dto.Version;
        pkg.DllVersion = dto.DllVersion; pkg.StoragePath = dto.StoragePath;
        pkg.IsActive = dto.IsActive; pkg.IsRollbackPackage = dto.IsRollbackPackage;
        pkg.FYYear = dto.FYYear;
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true });
    }

    [HttpPatch("Packages/{id}/activate")]
    public async Task<JsonResult> ActivatePackage(Guid id, CancellationToken ct)
    {
        var pkg = await _db.ScriptPackages.FindAsync(new object[]{id}, ct);
        if (pkg == null) return Json(new { success = false });
        // Deactivate others of same step type + FY year
        var others = await _db.ScriptPackages
            .Where(p => p.StepType == pkg.StepType && p.FYYear == pkg.FYYear && p.PackageId != id)
            .ToListAsync(ct);
        others.ForEach(p => p.IsActive = false);
        pkg.IsActive = true;
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true });
    }

    [HttpDelete("Packages/{id}")]
    public async Task<JsonResult> DeletePackage(Guid id, CancellationToken ct)
    {
        var pkg = await _db.ScriptPackages.FindAsync(new object[]{id}, ct);
        if (pkg == null) return Json(new { success = false });
        pkg.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true });
    }

    // ══════════════════════════════════════════════════════════════════════
    // HEARTBEATS (read-only with filter/delete)
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("Heartbeats")]
    public async Task<JsonResult> GetHeartbeats(
        Guid? terminalId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var q = _db.Heartbeats.Include(h => h.Terminal).ThenInclude(t => t.Store).AsQueryable();
        if (terminalId.HasValue) q = q.Where(h => h.TerminalId == terminalId.Value);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(h => h.ReceivedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(h => new {
                h.HeartbeatId, h.TerminalId,
                TerminalCode = h.Terminal.TerminalCode,
                StoreCode = h.Terminal.Store.StoreCode,
                h.ReceivedAt, h.AgentVersion, h.Status,
                h.DiskFreeGB, h.PosProcessRunning, h.LocalTime
            }).ToListAsync(ct);
        return Json(new { total, page, pageSize, items });
    }

    [HttpDelete("Heartbeats/purge")]
    public async Task<JsonResult> PurgeOldHeartbeats([FromBody] PurgeDto dto, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-dto.OlderThanDays);
        var deleted = await _db.Heartbeats.Where(h => h.ReceivedAt < cutoff).ExecuteDeleteAsync(ct);
        return Json(new { success = true, deleted });
    }

    // ══════════════════════════════════════════════════════════════════════
    // AUDIT LOGS (read-only)
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("AuditLogs")]
    public async Task<JsonResult> GetAuditLogs(
        string? action, string? entityType, string? userId,
        DateTime? from, DateTime? to,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var q = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(action))     q = q.Where(a => a.Action.Contains(action));
        if (!string.IsNullOrEmpty(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrEmpty(userId))     q = q.Where(a => a.UserId.Contains(userId));
        if (from.HasValue) q = q.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue)   q = q.Where(a => a.Timestamp <= to.Value);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.Timestamp)
            .Skip((page-1)*pageSize).Take(pageSize)
            .ToListAsync(ct);
        return Json(new { total, page, pageSize, items });
    }

    // ══════════════════════════════════════════════════════════════════════
    // DASHBOARD SUMMARY
    // ══════════════════════════════════════════════════════════════════════
    [HttpGet("Summary")]
    public async Task<JsonResult> Summary(CancellationToken ct)
    {
        return Json(new {
            stores = new {
                total = await _db.Stores.CountAsync(ct),
                active = await _db.Stores.CountAsync(s => s.Status == StoreStatus.Active, ct),
                inactive = await _db.Stores.CountAsync(s => s.Status == StoreStatus.Inactive, ct),
                fyCompleted = await _db.Stores.CountAsync(s => s.FYCloseStatus == FYCloseStatus.Completed, ct),
                fyFailed = await _db.Stores.CountAsync(s => s.FYCloseStatus == FYCloseStatus.Failed, ct),
                fyPending = await _db.Stores.CountAsync(s => s.FYCloseStatus == FYCloseStatus.Pending, ct),
            },
            terminals = new {
                total = await _db.Terminals.CountAsync(ct),
                online = await _db.Terminals.CountAsync(t => t.Status == TerminalStatus.Active, ct),
                offline = await _db.Terminals.CountAsync(t => t.Status == TerminalStatus.Offline, ct),
                locked = await _db.Terminals.CountAsync(t => t.Status == TerminalStatus.Locked, ct),
            },
            commands = new {
                total = await _db.Commands.CountAsync(ct),
                queued = await _db.Commands.CountAsync(c => c.Status == CommandStatus.Queued, ct),
                running = await _db.Commands.CountAsync(c => c.Status == CommandStatus.Running, ct),
                success = await _db.Commands.CountAsync(c => c.Status == CommandStatus.Success, ct),
                failed = await _db.Commands.CountAsync(c => c.Status == CommandStatus.Failed, ct),
            },
            packages = new {
                total = await _db.ScriptPackages.CountAsync(ct),
                active = await _db.ScriptPackages.CountAsync(p => p.IsActive, ct),
            },
            heartbeats = new {
                total = await _db.Heartbeats.CountAsync(ct),
                last24h = await _db.Heartbeats.CountAsync(h => h.ReceivedAt > DateTime.UtcNow.AddHours(-24), ct),
            },
            auditLogs = new {
                total = await _db.AuditLogs.CountAsync(ct),
                today = await _db.AuditLogs.CountAsync(a => a.Timestamp > DateTime.UtcNow.Date, ct),
            }
        });
    }
}

// ── DTOs ────────────────────────────────────────────────────────────────────
public record StoreUpsertDto(
    string StoreCode, string StoreName, string Region, string Zone,
    int Priority, string Status, string? ContactEmail, string? ContactPhone);

public record StatusDto(string Status, string? FYCloseStatus = null);

public record TerminalUpdateDto(
    string? IpAddress, string? AgentVersion, string? PosVersion, bool IsPrimary);

public record FYJobUpdateDto(
    int WaveSize, int WaveIntervalMinutes,
    DateTime ExecutionWindowStart, DateTime ExecutionWindowEnd);

public record PackageCreateDto(
    string PackageName, string StepType, string Version, string? DllVersion,
    long FileSize, string Sha256Hash, string StoragePath,
    bool IsActive, bool IsRollbackPackage, string? FYYear);

public record PurgeDto(int OlderThanDays);
