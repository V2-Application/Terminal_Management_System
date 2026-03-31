using HO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text.Json;

namespace HO.API.Controllers;

/// <summary>
/// Serves the Store Agent installer package.
/// GET /api/v1/agent/download — returns agent.zip for new terminal installations.
/// GET /api/v1/agent/config?storeCode=ST001 — returns preconfigured appsettings.json
/// </summary>
[ApiController]
[Route("api/v1/agent")]
public class AgentController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly AppDbContext _db;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IWebHostEnvironment env, AppDbContext db,
        ILogger<AgentController> logger)
    {
        _env    = env;
        _db     = db;
        _logger = logger;
    }

    /// <summary>
    /// Download the Store Agent zip package.
    /// File must be placed at: wwwroot/agent/StoreAgent.zip
    /// </summary>
    [HttpGet("download")]
    [AllowAnonymous]  // Must be downloadable before terminal is registered
    public IActionResult Download()
    {
        var agentZipPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "agent", "StoreAgent.zip");

        if (!System.IO.File.Exists(agentZipPath))
        {
            _logger.LogWarning("Agent package not found at {Path}", agentZipPath);
            return NotFound(new {
                error = "Agent package not yet published.",
                instructions = "Build and publish Store.Agent project, then copy to wwwroot/agent/StoreAgent.zip",
                devGuide = "See docs/STORE_AGENT_INSTALL.md"
            });
        }

        _logger.LogInformation("Agent package downloaded from {IP}",
            HttpContext.Connection.RemoteIpAddress);

        return PhysicalFile(
            agentZipPath,
            "application/zip",
            "StoreAgent.zip");
    }

    /// <summary>
    /// Returns a preconfigured appsettings.json for a specific store.
    /// Used in the automated installer to avoid manual configuration.
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    public async Task<IActionResult> GetConfig(string storeCode, CancellationToken ct)
    {
        var store = await _db.Stores
            .FirstOrDefaultAsync(s => s.StoreCode == storeCode && !s.IsDeleted, ct);

        if (store == null)
            return NotFound(new { error = $"Store '{storeCode}' not found." });

        // Build the API base URL from the current request
        var apiBase = $"{Request.Scheme}://{Request.Host}/api/v1";

        var config = new
        {
            AgentConfig = new
            {
                HoApiBaseUrl             = apiBase,
                StoreCode                = storeCode,
                PollIntervalSeconds      = 60,
                HeartbeatIntervalSeconds = 300,
                MaxConcurrentCommands    = 1,
                ExecutionTimeoutSeconds  = 600,
                LocalPackageCacheDir     = @"C:\RetailTMS\PackageCache",
                LocalLogDir              = @"C:\RetailTMS\Logs",
                LocalDbPath              = @"C:\RetailTMS\agent.db",
                PosExecutablePath        = @"C:\Program Files (x86)\Microsoft Dynamics AX\60\Retail POS\POS.exe",
                PosExtensionsPath        = @"C:\Program Files (x86)\Microsoft Dynamics AX\60\Retail POS\Extensions",
                IsolatedStoragePath      = @"C:\ProgramData\IsolatedStorage",
            },
            Serilog = new { MinimumLevel = "Information" }
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null  // keep PascalCase
        });

        return Content(json, "application/json");
    }

    /// <summary>
    /// Returns current agent version info
    /// </summary>
    [HttpGet("version")]
    [AllowAnonymous]
    public IActionResult Version()
    {
        var agentZipPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "agent", "StoreAgent.zip");
        return Ok(new {
            version         = "1.0.0",
            packageAvailable = System.IO.File.Exists(agentZipPath),
            downloadUrl     = $"{Request.Scheme}://{Request.Host}/api/v1/agent/download",
            configUrl       = $"{Request.Scheme}://{Request.Host}/api/v1/agent/config?storeCode={{STORE_CODE}}",
            publishedAt     = System.IO.File.Exists(agentZipPath)
                ? System.IO.File.GetLastWriteTime(agentZipPath).ToString("yyyy-MM-dd HH:mm")
                : null
        });
    }
}
