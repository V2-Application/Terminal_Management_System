using HO.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HO.Web.Controllers;

[Authorize]
public class TerminalController : Controller
{
    private readonly ITerminalRepository _terminals;
    private readonly IStoreRepository    _stores;

    public TerminalController(ITerminalRepository terminals, IStoreRepository stores)
    {
        _terminals = terminals; _stores = stores;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var terminals = await _terminals.GetActiveTerminalsAsync(ct);
        return View(terminals);
    }

    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var offline  = await _terminals.GetOfflineTerminalsAsync(TimeSpan.FromMinutes(10), ct);
        var active   = await _terminals.GetActiveTerminalsAsync(ct);
        ViewBag.Offline = offline;
        return View(active);
    }
}
