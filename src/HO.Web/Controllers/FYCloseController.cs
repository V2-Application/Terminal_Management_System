using HO.Application.Interfaces;
using HO.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HO.Web.Controllers;

[Authorize(Roles = "SuperAdmin,HOAdmin")]
public class FYCloseController : Controller
{
    private readonly IFYCloseService _fyCloseService;
    private readonly IFYJobRepository _fyJobRepo;
    private readonly IPackageRepository _packageRepo;

    public FYCloseController(
        IFYCloseService fyCloseService,
        IFYJobRepository fyJobRepo,
        IPackageRepository packageRepo)
    {
        _fyCloseService = fyCloseService;
        _fyJobRepo = fyJobRepo;
        _packageRepo = packageRepo;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var activeJob = await _fyJobRepo.GetActiveJobAsync(ct);
        var packages = await _packageRepo.GetAllAsync(ct);
        ViewBag.ActiveJob = activeJob;
        ViewBag.Packages = packages.Where(p => !p.IsDeleted && p.StepType == "FY_CLOSE").ToList();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(
        string fyYear, Guid scriptPackageId, Guid rollbackPackageId,
        int waveSize, DateTime windowStart, DateTime windowEnd,
        CancellationToken ct)
    {
        var user = User.Identity?.Name ?? "UNKNOWN";
        var job = await _fyCloseService.StartJobAsync(
            fyYear, scriptPackageId, rollbackPackageId,
            waveSize, windowStart, windowEnd, user, ct);

        TempData["Success"] = $"FY-Close batch for {fyYear} started. Job ID: {job.FYJobId}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pause(Guid fyJobId, CancellationToken ct)
    {
        await _fyCloseService.PauseJobAsync(fyJobId, ct);
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resume(Guid fyJobId, CancellationToken ct)
    {
        await _fyCloseService.ResumeJobAsync(fyJobId, ct);
        return Json(new { success = true });
    }
}
