using HO.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HO.API.Controllers;

[ApiController]
[Route("api/v1/packages")]
public class PackagesController : ControllerBase
{
    private readonly IPackageRepository _packageRepo;
    private readonly ILogger<PackagesController> _logger;

    public PackagesController(IPackageRepository packageRepo, ILogger<PackagesController> logger)
    {
        _packageRepo = packageRepo;
        _logger = logger;
    }

    /// <summary>
    /// Agent downloads a specific script package (ZIP).
    /// Requires terminal JWT. Package must be in Active or Rollback state.
    /// </summary>
    [HttpGet("{packageId:guid}/download")]
    [Authorize(Policy = "TerminalPolicy")]
    public async Task<IActionResult> Download(Guid packageId, CancellationToken ct)
    {
        var package = await _packageRepo.GetByIdAsync(packageId, ct);
        if (package == null || package.IsDeleted) return NotFound();

        if (!System.IO.File.Exists(package.StoragePath))
        {
            _logger.LogError("Package file not found on disk: {Path}", package.StoragePath);
            return StatusCode(500, "Package file unavailable.");
        }

        var stream = System.IO.File.OpenRead(package.StoragePath);
        return File(stream, "application/zip", $"{package.PackageName}.zip");
    }

    /// <summary>
    /// Get active package metadata for a step type. Agent uses this to check if it has latest version.
    /// </summary>
    [HttpGet("active")]
    [Authorize(Policy = "TerminalPolicy")]
    public async Task<IActionResult> GetActive([FromQuery] string stepType, [FromQuery] string fyYear, CancellationToken ct)
    {
        var package = await _packageRepo.GetActivePackageAsync(stepType, fyYear, ct);
        if (package == null) return NotFound();

        return Ok(new
        {
            package.PackageId,
            package.PackageName,
            package.Version,
            package.Sha256Hash,
            package.FileSize
        });
    }
}
