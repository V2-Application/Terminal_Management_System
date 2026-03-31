using HO.Application.Interfaces;
using HO.Application.Services;
using HO.Domain.Entities;
using HO.Domain.Enums;
using HO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HO.Web.Controllers;

[Authorize]
public class StoreController : Controller
{
    private readonly IStoreRepository _storeRepo;
    private readonly ICommandService  _commandService;
    private readonly AppDbContext     _db;

    public StoreController(
        IStoreRepository storeRepo,
        ICommandService commandService,
        AppDbContext db)
    {
        _storeRepo      = storeRepo;
        _commandService = commandService;
        _db             = db;
    }

    // ── MVC Views ────────────────────────────────────────────────────────
    public async Task<IActionResult> Index(
        [FromQuery] string? region, CancellationToken ct)
    {
        var stores = string.IsNullOrWhiteSpace(region)
            ? await _storeRepo.GetAllAsync(ct: ct)
            : await _storeRepo.GetByRegionAsync(region, ct);
        ViewBag.Region = region;
        return View(stores);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var store = await _storeRepo.GetByIdAsync(id, ct);
        if (store == null) return NotFound();
        return View(store);
    }

    // ── AJAX partial grid ────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GridData(
        string? status, string? region, string? search,
        CancellationToken ct)
    {
        var stores = string.IsNullOrWhiteSpace(region)
            ? await _storeRepo.GetAllAsync(ct: ct)
            : await _storeRepo.GetByRegionAsync(region, ct);

        if (!string.IsNullOrWhiteSpace(status))
            stores = stores.Where(s => s.FYCloseStatus.ToString() == status);

        if (!string.IsNullOrWhiteSpace(search))
            stores = stores.Where(s =>
                s.StoreCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.StoreName.Contains(search, StringComparison.OrdinalIgnoreCase));

        return PartialView("_StoreGrid", stores);
    }

    // ══════════════════════════════════════════════════════════════════════
    // REST API  (called by AJAX from Index view and Admin panel)
    // ══════════════════════════════════════════════════════════════════════

    // GET /Store/Api/All
    [HttpGet("Store/Api/All")]
    public async Task<JsonResult> ApiGetAll(
        string? region, string? status, string? search,
        int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var q = _db.Stores.Include(s => s.Terminals).AsQueryable();

        if (!string.IsNullOrEmpty(region)) q = q.Where(s => s.Region == region);
        if (!string.IsNullOrEmpty(search))
            q = q.Where(s => s.StoreCode.Contains(search) || s.StoreName.Contains(search));
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<StoreStatus>(status, out var ss))
            q = q.Where(s => s.Status == ss);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(s => s.Region).ThenBy(s => s.StoreCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new {
                s.StoreId, s.StoreCode, s.StoreName, s.Region, s.Zone,
                s.Priority,
                status        = s.Status.ToString(),
                fyCloseStatus = s.FYCloseStatus.ToString(),
                s.ContactEmail, s.ContactPhone,
                s.IsDeleted, s.CreatedAt, s.UpdatedAt,
                terminalCount   = s.Terminals.Count,
                activeTerminals = s.Terminals.Count(t => t.Status == TerminalStatus.Active)
            }).ToListAsync(ct);

        return Json(new { total, page, pageSize, items });
    }

    // GET /Store/Api/{id}
    [HttpGet("Store/Api/{id}")]
    public async Task<JsonResult> ApiGet(Guid id, CancellationToken ct)
    {
        var s = await _db.Stores
            .Include(x => x.Terminals)
            .FirstOrDefaultAsync(x => x.StoreId == id, ct);
        if (s == null) return Json(new { error = "Not found" });
        return Json(new {
            s.StoreId, s.StoreCode, s.StoreName, s.Region, s.Zone,
            s.Priority, s.ContactEmail, s.ContactPhone,
            status        = s.Status.ToString(),
            fyCloseStatus = s.FYCloseStatus.ToString(),
            s.CreatedAt, s.UpdatedAt,
            terminals = s.Terminals.Select(t => new {
                t.TerminalId, t.TerminalCode,
                status = t.Status.ToString(),
                t.IsPrimary, t.LastHeartbeat, t.AgentVersion
            })
        });
    }

    // POST /Store/Api/Create
    [HttpPost("Store/Api/Create")]
    [ValidateAntiForgeryToken]
    public async Task<JsonResult> ApiCreate(
        [FromBody] StoreCreateDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.StoreCode) ||
            string.IsNullOrWhiteSpace(dto.StoreName))
            return Json(new { success = false, error = "Store Code and Name are required." });

        if (await _db.Stores.AnyAsync(s => s.StoreCode == dto.StoreCode.Trim().ToUpper(), ct))
            return Json(new { success = false, error = $"Store code '{dto.StoreCode}' already exists." });

        var store = new Store
        {
            StoreCode    = dto.StoreCode.Trim().ToUpper(),
            StoreName    = dto.StoreName.Trim(),
            Region       = dto.Region,
            Zone         = dto.Zone ?? string.Empty,
            Priority     = dto.Priority,
            ContactEmail = dto.ContactEmail,
            ContactPhone = dto.ContactPhone,
            Status       = StoreStatus.Active,
            FYCloseStatus = FYCloseStatus.Pending,
            CreatedBy    = User.Identity?.Name ?? "ADMIN",
        };

        _db.Stores.Add(store);
        await _db.SaveChangesAsync(ct);

        return Json(new {
            success = true,
            storeId = store.StoreId,
            storeCode = store.StoreCode,
            message = $"Store {store.StoreCode} — {store.StoreName} created successfully."
        });
    }

    // PUT /Store/Api/Update/{id}
    [HttpPut("Store/Api/Update/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<JsonResult> ApiUpdate(
        Guid id, [FromBody] StoreUpdateDto dto, CancellationToken ct)
    {
        var store = await _db.Stores.FindAsync(new object[] { id }, ct);
        if (store == null) return Json(new { success = false, error = "Store not found." });

        store.StoreName    = dto.StoreName.Trim();
        store.Region       = dto.Region;
        store.Zone         = dto.Zone ?? string.Empty;
        store.Priority     = dto.Priority;
        store.ContactEmail = dto.ContactEmail;
        store.ContactPhone = dto.ContactPhone;

        if (Enum.TryParse<StoreStatus>(dto.Status, out var newStatus))
            store.Status = newStatus;
        if (Enum.TryParse<FYCloseStatus>(dto.FYCloseStatus, out var newFY))
            store.FYCloseStatus = newFY;

        store.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Json(new { success = true, message = $"Store {store.StoreCode} updated." });
    }

    // PATCH /Store/Api/SetStatus/{id}
    [HttpPatch("Store/Api/SetStatus/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<JsonResult> ApiSetStatus(
        Guid id, [FromBody] StoreStatusDto dto, CancellationToken ct)
    {
        var store = await _db.Stores.FindAsync(new object[] { id }, ct);
        if (store == null) return Json(new { success = false, error = "Not found." });

        if (!string.IsNullOrEmpty(dto.Status) &&
            Enum.TryParse<StoreStatus>(dto.Status, out var s))
            store.Status = s;

        if (!string.IsNullOrEmpty(dto.FYCloseStatus) &&
            Enum.TryParse<FYCloseStatus>(dto.FYCloseStatus, out var fy))
            store.FYCloseStatus = fy;

        if (dto.Priority.HasValue)
            store.Priority = dto.Priority.Value;

        store.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Json(new {
            success = true,
            message = $"Store {store.StoreCode} status updated."
        });
    }

    // DELETE /Store/Api/Delete/{id}  (soft delete)
    [HttpDelete("Store/Api/Delete/{id}")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,HOAdmin")]
    public async Task<JsonResult> ApiDelete(Guid id, CancellationToken ct)
    {
        var store = await _db.Stores.FindAsync(new object[] { id }, ct);
        if (store == null) return Json(new { success = false, error = "Not found." });

        // Check if store has active commands
        var hasRunning = await _db.Commands.AnyAsync(c =>
            c.StoreId == id &&
            (c.Status == CommandStatus.Running || c.Status == CommandStatus.Queued), ct);

        if (hasRunning)
            return Json(new {
                success = false,
                error = "Cannot delete store with active/queued commands. Cancel commands first."
            });

        store.IsDeleted = true;
        store.Status    = StoreStatus.Decommissioned;
        store.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Json(new {
            success = true,
            message = $"Store {store.StoreCode} decommissioned."
        });
    }

    // ── Legacy MVC POST actions ──────────────────────────────────────────
    [Authorize(Roles = "SuperAdmin,HOAdmin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(Guid commandId, CancellationToken ct)
    {
        await _commandService.RetryCommandAsync(commandId, ct);
        return Json(new { success = true });
    }

    [Authorize(Roles = "SuperAdmin,HOAdmin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rollback(
        Guid storeId, Guid fyJobId, CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "UNKNOWN";
        await _commandService.RollbackStoreAsync(storeId, fyJobId, user, ct);
        return Json(new { success = true });
    }
}

// ── Request DTOs ────────────────────────────────────────────────────────────
public record StoreCreateDto(
    string StoreCode, string StoreName, string Region,
    string? Zone, int Priority,
    string? ContactEmail, string? ContactPhone);

public record StoreUpdateDto(
    string StoreName, string Region, string? Zone, int Priority,
    string Status, string FYCloseStatus,
    string? ContactEmail, string? ContactPhone);

public record StoreStatusDto(
    string? Status, string? FYCloseStatus, int? Priority);
