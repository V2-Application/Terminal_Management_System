using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HO.Web.Controllers;

public class AccountController : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl)
    {
        // TODO: Replace with real user lookup from AspNetCore.Identity or AD
        // This is a placeholder for initial development
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name,           username),
                new(ClaimTypes.Role,           "HOAdmin"),  // TODO: load from DB
                new("UserId",                  Guid.NewGuid().ToString()),
            };

            var identity  = new ClaimsIdentity(claims, "Cookies");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("Cookies", principal);

            return Redirect(returnUrl ?? "/");
        }

        ViewBag.Error     = "Invalid username or password.";
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Login");
    }
}
