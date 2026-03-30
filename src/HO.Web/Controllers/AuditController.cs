using HO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HO.Web.Controllers;

[Authorize]
public class AuditController : Controller
{
    private readonly AppDbContext _db;
    public AuditController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(
        string? action, string? entityType, DateTime? from, DateTime? to,
        int page = 1, CancellationToken ct = default)
    {
        const int pageSize = 50;
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))      query = query.Where(a => a.Action.Contains(action));
        if (!string.IsNullOrWhiteSpace(entityType))  query = query.Where(a => a.EntityType == entityType);
        if (from.HasValue) query = query.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue)   query = query.Where(a => a.Timestamp <= to.Value);

        var total = await query.CountAsync(ct);
        var logs  = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        ViewBag.Total    = total;
        ViewBag.Page     = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Pages    = (int)Math.Ceiling((double)total / pageSize);
        return View(logs);
    }
}
