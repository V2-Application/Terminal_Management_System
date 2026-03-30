using HO.Application.Interfaces;
using HO.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HO.Web.Controllers;

[Authorize]
public class StoreController : Controller
{
    private readonly IStoreRepository _storeRepo;
    private readonly ICommandService  _commandService;

    public StoreController(IStoreRepository storeRepo, ICommandService commandService)
    {
        _storeRepo      = storeRepo;
        _commandService = commandService;
    }

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

    /// <summary>AJAX endpoint — returns partial HTML for the live store grid.</summary>
    [HttpGet]
    public async Task<IActionResult> GridData(
        string? status, string? region, CancellationToken ct)
    {
        var stores = string.IsNullOrWhiteSpace(region)
            ? await _storeRepo.GetAllAsync(ct: ct)
            : await _storeRepo.GetByRegionAsync(region, ct);

        if (!string.IsNullOrWhiteSpace(status))
            stores = stores.Where(s => s.FYCloseStatus.ToString() == status);

        return PartialView("_StoreGrid", stores);
    }

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
