using HO.Application.Interfaces;
using HO.Application.Services;
using HO.Domain.Enums;
using HO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HO.Web.Controllers;

[Authorize]
public class TerminalController : Controller
{
    private readonly ITerminalRepository _terminals;
    private readonly IStoreRepository    _stores;
    private readonly ICommandService     _commands;
    private readonly IPackageRepository  _packages;
    private readonly AppDbContext        _db;

    public TerminalController(
        ITerminalRepository terminals, IStoreRepository stores,
        ICommandService commands, IPackageRepository packages,
        AppDbContext db)
    {
        _terminals = terminals; _stores = stores;
        _commands = commands; _packages = packages; _db = db;
    }

    // ── List all terminals ──────────────────────────────────────────────────
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var terminals = await _db.Terminals
            .Include(t => t.Store)
            .OrderBy(t => t.Store.Region)
            .ThenBy(t => t.TerminalCode)
            .ToListAsync(ct);
        return View(terminals);
    }

    // ── Terminal detail + execute panel ────────────────────────────────────
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var terminal = await _db.Terminals
            .Include(t => t.Store)
            .Include(t => t.Commands.OrderByDescending(c => c.CreatedAt).Take(10))
                .ThenInclude(c => c.Executions)
            .FirstOrDefaultAsync(t => t.TerminalId == id, ct);

        if (terminal == null) return NotFound();

        var packages = await _packages.GetAllAsync(ct);
        ViewBag.Packages = packages.Where(p => p.IsActive || p.IsRollbackPackage).ToList();

        // Heartbeat history (last 10)
        var heartbeats = await _db.Heartbeats
            .Where(h => h.TerminalId == id)
            .OrderByDescending(h => h.ReceivedAt)
            .Take(10)
            .ToListAsync(ct);
        ViewBag.Heartbeats = heartbeats;

        return View(terminal);
    }

    // ── PING — sends a synthetic heartbeat check ────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<JsonResult> Ping(Guid terminalId, CancellationToken ct)
    {
        var terminal = await _terminals.GetByIdAsync(terminalId, ct);
        if (terminal == null) return Json(new { success = false, message = "Terminal not found" });

        var isOnline  = terminal.Status == TerminalStatus.Active;
        var lastSeen  = terminal.LastHeartbeat;
        var agoSecs   = lastSeen.HasValue ? (int)(DateTime.UtcNow - lastSeen.Value).TotalSeconds : -1;
        var diskFree  = terminal.DiskFreeGB;

        return Json(new {
            success   = true,
            isOnline,
            status    = terminal.Status.ToString(),
            lastSeen  = lastSeen?.ToString("yyyy-MM-dd HH:mm:ss UTC"),
            agoSeconds = agoSecs,
            diskFreeGB = diskFree,
            agentVersion = terminal.AgentVersion,
            ipAddress = terminal.IpAddress,
            machineName = terminal.MachineName,
            message = isOnline
                ? $"✓ Online — last heartbeat {agoSecs}s ago"
                : $"✗ Offline — last seen {lastSeen?.ToString("HH:mm dd-MMM") ?? "never"}"
        });
    }

    // ── EXECUTE STEP — dispatches a single command to this terminal ─────────
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,HOAdmin,HOOperator")]
    public async Task<JsonResult> ExecuteStep(
        Guid terminalId, string commandType, Guid? packageId,
        CancellationToken ct)
    {
        var terminal = await _terminals.GetByIdAsync(terminalId, ct);
        if (terminal == null)
            return Json(new { success = false, message = "Terminal not found" });

        if (terminal.Status == TerminalStatus.Offline)
            return Json(new { success = false, message = "Terminal is OFFLINE — command will be queued and executed when it reconnects" });

        if (!Enum.TryParse<CommandType>(commandType, out var cmdType))
            return Json(new { success = false, message = "Unknown command type: " + commandType });

        var user = User.Identity?.Name ?? "HO_USER";

        var cmd = await _commands.CreateCommandAsync(
            terminalId, terminal.StoreId, cmdType,
            packageId, fyJobId: null, priority: 1, createdBy: user, ct);

        return Json(new {
            success = true,
            commandId = cmd.CommandId,
            message   = $"✓ Command '{cmdType}' dispatched — terminal will execute on next poll (≤60s)",
            status    = "Queued"
        });
    }

    // ── EXECUTE ALL 4 STEPS (full FY-close sequence) ────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "SuperAdmin,HOAdmin")]
    public async Task<JsonResult> ExecuteFullSequence(
        Guid terminalId, Guid? fyClosePackageId, CancellationToken ct)
    {
        var terminal = await _terminals.GetByIdAsync(terminalId, ct);
        if (terminal == null)
            return Json(new { success = false, message = "Terminal not found" });

        var user = User.Identity?.Name ?? "HO_USER";
        var created = new List<object>();

        // Step 1: NSO Setup (priority 1)
        var cmd1 = await _commands.CreateCommandAsync(
            terminalId, terminal.StoreId, CommandType.NsoSetup,
            null, null, 1, user, ct);
        created.Add(new { step = 1, name = "IT NSO Setup", commandId = cmd1.CommandId });

        // Step 2: FY Close (priority 2)
        var cmd2 = await _commands.CreateCommandAsync(
            terminalId, terminal.StoreId, CommandType.FyClose,
            fyClosePackageId, null, 2, user, ct);
        created.Add(new { step = 2, name = "FY Close", commandId = cmd2.CommandId });

        // Step 3: Time → Test server (priority 3)
        var cmd3 = await _commands.CreateCommandAsync(
            terminalId, terminal.StoreId, CommandType.TimeSyncTest,
            null, null, 3, user, ct);
        created.Add(new { step = 3, name = "Time Sync → Test", commandId = cmd3.CommandId });

        // Step 4: Time → Prod server (priority 4)
        var cmd4 = await _commands.CreateCommandAsync(
            terminalId, terminal.StoreId, CommandType.TimeSyncProd,
            null, null, 4, user, ct);
        created.Add(new { step = 4, name = "Time Sync → Prod", commandId = cmd4.CommandId });

        return Json(new {
            success  = true,
            commands = created,
            message  = "✓ All 4 steps queued — terminal will execute them in order (≤60s per step)"
        });
    }

    // ── Live command status (AJAX polling) ─────────────────────────────────
    [HttpGet]
    public async Task<JsonResult> CommandStatus(Guid commandId, CancellationToken ct)
    {
        var cmd = await _db.Commands
            .Include(c => c.Executions.OrderByDescending(e => e.StartedAt).Take(1))
            .FirstOrDefaultAsync(c => c.CommandId == commandId, ct);

        if (cmd == null) return Json(new { found = false });

        var lastExec = cmd.Executions.FirstOrDefault();
        return Json(new {
            found       = true,
            commandId,
            status      = cmd.Status.ToString(),
            createdAt   = cmd.CreatedAt.ToString("HH:mm:ss"),
            dispatchedAt = cmd.DispatchedAt?.ToString("HH:mm:ss"),
            startedAt   = cmd.StartedAt?.ToString("HH:mm:ss"),
            completedAt = cmd.CompletedAt?.ToString("HH:mm:ss"),
            exitCode    = lastExec?.ExitCode,
            stdout      = lastExec?.Stdout?.Length > 2000 ? lastExec.Stdout[^2000..] : lastExec?.Stdout,
            stderr      = lastExec?.Stderr?.Length > 1000 ? lastExec.Stderr[^1000..] : lastExec?.Stderr,
            durationMs  = lastExec?.DurationMs
        });
    }

    // ── Bind page — shows installation instructions for a store ────────────
    public async Task<IActionResult> Bind(Guid storeId, CancellationToken ct)
    {
        var store = await _stores.GetByIdAsync(storeId, ct);
        if (store == null) return NotFound();
        ViewBag.Store = store;
        return View();
    }
}
