using HO.Application.DTOs;
using HO.Application.Interfaces;
using HO.Application.Queries.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HO.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IMediator        _mediator;
    private readonly IFYJobRepository _fyJobRepo;

    public DashboardController(IMediator mediator, IFYJobRepository fyJobRepo)
    {
        _mediator  = mediator;
        _fyJobRepo = fyJobRepo;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            var activeJob = await _fyJobRepo.GetActiveJobAsync(ct);
            var summary   = await _mediator.Send(
                new GetDashboardSummaryQuery(activeJob?.FYJobId), ct);
            return View(summary);
        }
        catch (Exception ex)
        {
            // DB not ready yet — show empty dashboard with a notice
            TempData["Error"] = "Database is initialising — please refresh in a moment. (" + ex.Message[..Math.Min(100, ex.Message.Length)] + ")";
            return View(new DashboardSummaryDto());
        }
    }

    [HttpGet]
    public async Task<JsonResult> Summary(CancellationToken ct)
    {
        try
        {
            var summary = await _mediator.Send(new GetDashboardSummaryQuery(null), ct);
            return Json(summary);
        }
        catch
        {
            return Json(new DashboardSummaryDto());
        }
    }
}
