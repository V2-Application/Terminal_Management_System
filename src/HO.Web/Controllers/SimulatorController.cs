using HO.Domain.Entities;
using HO.Domain.Enums;
using HO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HO.Web.Controllers;

/// <summary>
/// Terminal Simulator — lets you test the full pipeline without a real store PC.
/// Registers a virtual terminal, sends heartbeats, and dispatches fake command results.
/// Only enabled in development. Shows data live on the dashboard.
/// </summary>
[Authorize]
[Route("Simulator")]
public class SimulatorController : Controller
{
    private readonly AppDbContext _db;
    public SimulatorController(AppDbContext db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Load all stores for the dropdown
        var stores = await _db.Stores
            .OrderBy(s => s.StoreCode)
            .Select(s => new { s.StoreId, s.StoreCode, s.StoreName, s.Region })
            .ToListAsync(ct);
        ViewBag.Stores = stores;

        // Load existing virtual terminals
        var virtualTerminals = await _db.Terminals
            .Include(t => t.Store)
            .Where(t => t.MachineId.StartsWith("VIRTUAL-"))
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .ToListAsync(ct);
        ViewBag.VirtualTerminals = virtualTerminals;

        return View();
    }

    // ── Step 1: Register a virtual terminal ────────────────────────────────
    [HttpPost("Register")]
    public async Task<JsonResult> RegisterVirtualTerminal(
        Guid storeId, string? terminalLabel, CancellationToken ct)
    {
        var store = await _db.Stores.FindAsync(new object[] { storeId }, ct);
        if (store == null) return Json(new { success = false, error = "Store not found" });

        // Check if virtual terminal already exists for this store
        var existing = await _db.Terminals
            .FirstOrDefaultAsync(t => t.StoreId == storeId && t.MachineId.StartsWith("VIRTUAL-"), ct);

        if (existing != null)
        {
            existing.Status = TerminalStatus.Active;
            existing.LastHeartbeat = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return Json(new {
                success = true,
                terminalId = existing.TerminalId,
                terminalCode = existing.TerminalCode,
                message = "Existing virtual terminal reactivated"
            });
        }

        // Count existing terminals for this store
        var count = await _db.Terminals.CountAsync(t => t.StoreId == storeId, ct);
        var label = terminalLabel ?? (store.StoreCode + "-T" + (count + 1).ToString("D2"));

        var terminal = new Terminal
        {
            StoreId      = storeId,
            TerminalCode = label,
            MachineId    = "VIRTUAL-" + Guid.NewGuid().ToString("N")[..12].ToUpper(),
            MachineName  = label + "-PC",
            IpAddress    = "192.168." + (new Random().Next(1, 254)) + "." + (new Random().Next(1, 254)),
            OsVersion    = "Windows 10 Pro 22H2",
            AgentVersion = "1.0.0-SIM",
            PosVersion   = "AX2012 R3",
            Status       = TerminalStatus.Active,
            IsPrimary    = count == 0,
            LastHeartbeat = DateTime.UtcNow,
            DiskFreeGB   = 85.4m,
        };

        _db.Terminals.Add(terminal);

        // Update store status
        store.FYCloseStatus = FYCloseStatus.Pending;
        store.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Json(new {
            success = true,
            terminalId = terminal.TerminalId,
            terminalCode = terminal.TerminalCode,
            storeCode = store.StoreCode,
            message = "Virtual terminal registered and ONLINE"
        });
    }

    // ── Step 2: Send a heartbeat (makes terminal appear ONLINE) ────────────
    [HttpPost("Heartbeat")]
    public async Task<JsonResult> SendHeartbeat(
        Guid terminalId, decimal diskFreeGB = 85.4m,
        bool posRunning = true, CancellationToken ct = default)
    {
        var terminal = await _db.Terminals.FindAsync(new object[] { terminalId }, ct);
        if (terminal == null) return Json(new { success = false, error = "Terminal not found" });

        terminal.LastHeartbeat = DateTime.UtcNow;
        terminal.DiskFreeGB    = diskFreeGB;
        terminal.Status        = TerminalStatus.Active;
        terminal.UpdatedAt     = DateTime.UtcNow;

        _db.Heartbeats.Add(new Heartbeat
        {
            TerminalId        = terminalId,
            ReceivedAt        = DateTime.UtcNow,
            AgentVersion      = terminal.AgentVersion,
            Status            = "ACTIVE",
            DiskFreeGB        = diskFreeGB,
            PosProcessRunning = posRunning,
            LocalTime         = DateTime.Now
        });

        await _db.SaveChangesAsync(ct);

        return Json(new {
            success      = true,
            terminalId,
            status       = "Active",
            lastHeartbeat = DateTime.UtcNow.ToString("HH:mm:ss dd-MMM"),
            diskFreeGB,
            message      = $"Heartbeat sent — terminal {terminal.TerminalCode} is ONLINE"
        });
    }

    // ── Step 3: Simulate a command execution (SUCCESS or FAIL) ─────────────
    [HttpPost("SimulateCommand")]
    public async Task<JsonResult> SimulateCommand(
        Guid terminalId, string commandType, string result = "Success",
        string? customOutput = null, CancellationToken ct = default)
    {
        var terminal = await _db.Terminals.Include(t => t.Store)
            .FirstOrDefaultAsync(t => t.TerminalId == terminalId, ct);
        if (terminal == null) return Json(new { success = false, error = "Terminal not found" });

        if (!Enum.TryParse<CommandType>(commandType, out var cmdType))
            return Json(new { success = false, error = "Unknown command type" });

        var succeeded = result == "Success";
        var exitCode  = succeeded ? 0 : 1;

        // Create command record
        var cmd = new Command
        {
            TerminalId   = terminalId,
            StoreId      = terminal.StoreId,
            CommandType  = cmdType,
            Priority     = 1,
            CreatedBy    = "SIMULATOR",
            Status       = succeeded ? CommandStatus.Success : CommandStatus.Failed,
            ScheduledFor = DateTime.UtcNow,
            DispatchedAt = DateTime.UtcNow.AddSeconds(-10),
            StartedAt    = DateTime.UtcNow.AddSeconds(-8),
            CompletedAt  = DateTime.UtcNow,
        };
        _db.Commands.Add(cmd);
        await _db.SaveChangesAsync(ct);

        // Create execution record with realistic output
        var stdout = customOutput ?? SimulatedOutput(cmdType, terminal.Store.StoreCode, succeeded);
        _db.CommandExecutions.Add(new CommandExecution
        {
            CommandId     = cmd.CommandId,
            TerminalId    = terminalId,
            AttemptNumber = 1,
            ExitCode      = exitCode,
            Stdout        = stdout,
            Stderr        = succeeded ? "" : "ERROR: " + GetSimulatedError(cmdType),
            DurationMs    = new Random().Next(8000, 45000),
            StartedAt     = cmd.StartedAt!.Value,
            CompletedAt   = cmd.CompletedAt,
        });

        // Update store FY status
        if (succeeded && cmdType == CommandType.FyClose)
        {
            var store = await _db.Stores.FindAsync(new object[] { terminal.StoreId }, ct);
            if (store != null)
            {
                store.FYCloseStatus = FYCloseStatus.Completed;
                store.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        return Json(new {
            success = true,
            commandId = cmd.CommandId,
            commandType, result, exitCode,
            stdout = stdout[..Math.Min(200, stdout.Length)],
            message = $"{commandType} simulated as {result} on {terminal.TerminalCode}"
        });
    }

    // ── Step 4: Mark terminal OFFLINE ──────────────────────────────────────
    [HttpPost("GoOffline")]
    public async Task<JsonResult> GoOffline(Guid terminalId, CancellationToken ct)
    {
        var terminal = await _db.Terminals.FindAsync(new object[] { terminalId }, ct);
        if (terminal == null) return Json(new { success = false });
        terminal.Status    = TerminalStatus.Offline;
        terminal.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true, message = terminal.TerminalCode + " marked OFFLINE" });
    }

    // ── Bulk: simulate heartbeat for ALL active virtual terminals ──────────
    [HttpPost("HeartbeatAll")]
    public async Task<JsonResult> HeartbeatAll(CancellationToken ct)
    {
        var terminals = await _db.Terminals
            .Where(t => t.MachineId.StartsWith("VIRTUAL-"))
            .ToListAsync(ct);

        foreach (var t in terminals)
        {
            t.LastHeartbeat = DateTime.UtcNow;
            t.Status        = TerminalStatus.Active;
            t.UpdatedAt     = DateTime.UtcNow;
            _db.Heartbeats.Add(new Heartbeat {
                TerminalId = t.TerminalId, ReceivedAt = DateTime.UtcNow,
                AgentVersion = t.AgentVersion, Status = "ACTIVE",
                DiskFreeGB = t.DiskFreeGB, PosProcessRunning = true, LocalTime = DateTime.Now
            });
        }
        await _db.SaveChangesAsync(ct);
        return Json(new { success = true, count = terminals.Count, message = $"Sent heartbeat to {terminals.Count} virtual terminals" });
    }

    // ── Simulated realistic BAT output ──────────────────────────────────────
    private static string SimulatedOutput(CommandType type, string storeCode, bool success)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss");
        return type switch
        {
            CommandType.FyClose => success
                ? $"[{ts}] FY-CLOSE.BAT started for {storeCode}\r\n"
                + $"[{ts}] Stopping POS.exe... done (PID 4812)\r\n"
                + $"[{ts}] NET USE \\\\192.168.144.1\\DLLShare /user:admin ****\r\n"
                + $"[{ts}] Copying FY2025 DLLs to C:\\Program Files\\Microsoft Dynamics AX\\60\\Client\\Bin\\Extensions...\r\n"
                + $"[{ts}] Microsoft.Dynamics.AX.Framework.dll copied (1.2MB)\r\n"
                + $"[{ts}] Microsoft.Dynamics.AX.Metadata.dll copied (0.8MB)\r\n"
                + $"[{ts}] Clearing IsolatedStorage config cache...\r\n"
                + $"[{ts}] Config cache cleared\r\n"
                + $"[{ts}] FY-CLOSE.BAT completed successfully\r\nExit code: 0"
                : $"[{ts}] FY-CLOSE.BAT started for {storeCode}\r\n"
                + $"[{ts}] Stopping POS.exe... \r\n"
                + $"[{ts}] ERROR: Access denied copying DLLs\r\nExit code: 5",

            CommandType.NsoSetup => success
                ? $"[{ts}] IT_NSO_BATCH.BAT started\r\n"
                + $"[{ts}] Disabling Windows Firewall... OK\r\n"
                + $"[{ts}] Opening RDP port 1010... OK\r\n"
                + $"[{ts}] Setting timezone to IST (UTC+5:30)... OK\r\n"
                + $"[{ts}] Installing .NET 3.5 features... OK\r\n"
                + $"[{ts}] SAP config applied\r\n"
                + $"[{ts}] NSO setup complete\r\nExit code: 0"
                : $"[{ts}] IT_NSO_BATCH.BAT started\r\n"
                + $"[{ts}] ERROR: .NET 3.5 installation failed\r\nExit code: 1",

            CommandType.TimeSyncTest => success
                ? $"[{ts}] Time_set_before_Test_Bil.bat\r\n"
                + $"[{ts}] NET USE \\\\192.168.144.131\\IPC$ /user:Administrator ****\r\n"
                + $"[{ts}] The command completed successfully\r\n"
                + $"[{ts}] net time \\\\192.168.144.131 /set /yes\r\n"
                + $"[{ts}] Local time (\\\\192.168.144.131) is 30/03/2026 {ts}\r\n"
                + $"[{ts}] The command completed successfully\r\nExit code: 0"
                : $"[{ts}] ERROR: System error 5 has occurred. Access is denied.\r\nExit code: 5",

            CommandType.TimeSyncProd => success
                ? $"[{ts}] Time_set_After_Billing.bat\r\n"
                + $"[{ts}] NET USE \\\\192.168.144.158\\IPC$ /user:Administrator ****\r\n"
                + $"[{ts}] The command completed successfully\r\n"
                + $"[{ts}] net time \\\\192.168.144.158 /set /yes\r\n"
                + $"[{ts}] Local time is 30/03/2026 {ts}\r\n"
                + $"[{ts}] POS unlocked — billing active on production series\r\nExit code: 0"
                : $"[{ts}] ERROR: 53 — The network path was not found.\r\nExit code: 53",

            _ => $"[{ts}] Command executed\r\nExit code: {(success ? 0 : 1)}"
        };
    }

    private static string GetSimulatedError(CommandType type) => type switch
    {
        CommandType.FyClose     => "Exit code 5 — Access denied copying DLLs. Antivirus may be blocking.",
        CommandType.NsoSetup    => "Exit code 1 — .NET 3.5 installation failed.",
        CommandType.TimeSyncTest => "Exit code 5 — NET USE failed. Check credentials for 192.168.144.131.",
        CommandType.TimeSyncProd => "Exit code 53 — Network path not found. Is 192.168.144.158 reachable?",
        _ => "Command failed with exit code 1."
    };
}
