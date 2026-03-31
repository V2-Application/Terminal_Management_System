using HO.Domain.Entities;
using HO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HO.Web.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<AccountController> _logger;

    public AccountController(AppDbContext db, ILogger<AccountController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── GET /Account/Login ──────────────────────────────────────────────────
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(returnUrl ?? "/");

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    // ── POST /Account/Login ─────────────────────────────────────────────────
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        string username, string password, string? returnUrl,
        CancellationToken ct)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Username and password are required.";
            return View();
        }

        // Find user (case-insensitive username lookup)
        var user = await _db.HoUsers
            .FirstOrDefaultAsync(u =>
                u.Username.ToLower() == username.ToLower().Trim() &&
                u.IsActive, ct);

        if (user == null)
        {
            _logger.LogWarning("Login failed — unknown user: {Username}", username);
            ViewBag.Error = "Invalid username or password.";
            return View();
        }

        // Check account lockout
        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
        {
            var remaining = (int)(user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes + 1;
            ViewBag.Error = $"Account locked. Try again in {remaining} minute(s).";
            return View();
        }

        // Verify password with BCrypt
        bool passwordOk;
        try { passwordOk = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash); }
        catch { passwordOk = false; }

        if (!passwordOk)
        {
            user.FailedLoginCount++;

            // Lock after 5 failed attempts — 15 minutes
            if (user.FailedLoginCount >= 5)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
                _logger.LogWarning("Account locked after 5 failures: {Username}", username);
                ViewBag.Error = "Too many failed attempts. Account locked for 15 minutes.";
            }
            else
            {
                int remaining = 5 - user.FailedLoginCount;
                ViewBag.Error = $"Invalid username or password. {remaining} attempt(s) remaining.";
            }

            await _db.SaveChangesAsync(ct);
            return View();
        }

        // ✓ Password correct — update login tracking
        user.FailedLoginCount = 0;
        user.LockedUntil      = null;
        user.LastLoginAt      = DateTime.UtcNow;
        user.LastLoginIp      = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _db.SaveChangesAsync(ct);

        // Build claims principal
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name,               user.Username),
            new(ClaimTypes.Role,               user.Role),
            new(ClaimTypes.Email,              user.Email),
            new("FullName",                    user.FullName),
            new("UserId",                      user.UserId.ToString()),
            new("MustChangePassword",          user.MustChangePassword.ToString()),
        };

        var identity  = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("Cookies", principal);

        _logger.LogInformation("Login: {Username} ({Role}) from {IP}",
            user.Username, user.Role, user.LastLoginIp);

        // Redirect to change password if flagged
        if (user.MustChangePassword)
            return RedirectToAction(nameof(ChangePassword));

        return Redirect(returnUrl ?? "/");
    }

    // ── GET /Account/Logout ─────────────────────────────────────────────────
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        _logger.LogInformation("Logout: {User}", User.Identity?.Name);
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Login");
    }

    // ── GET /Account/ChangePassword ────────────────────────────────────────
    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View();

    // ── POST /Account/ChangePassword ───────────────────────────────────────
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        string currentPassword, string newPassword, string confirmPassword,
        CancellationToken ct)
    {
        if (newPassword != confirmPassword)
        {
            ViewBag.Error = "New passwords do not match.";
            return View();
        }

        if (newPassword.Length < 8)
        {
            ViewBag.Error = "Password must be at least 8 characters.";
            return View();
        }

        var userId = User.FindFirst("UserId")?.Value;
        var user   = await _db.HoUsers.FindAsync(new object[] { Guid.Parse(userId!) }, ct);

        if (user == null || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            ViewBag.Error = "Current password is incorrect.";
            return View();
        }

        user.PasswordHash      = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
        user.MustChangePassword = false;
        user.UpdatedAt         = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction("Index", "Dashboard");
    }

    // ── GET /Account/Profile ───────────────────────────────────────────────
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var userId = User.FindFirst("UserId")?.Value;
        var user   = await _db.HoUsers.FindAsync(new object[] { Guid.Parse(userId!) }, ct);
        return View(user);
    }
}
