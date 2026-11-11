using System.Security.Claims;
using GlowMe.Data;
using GlowMe.Support;
using GlowMe.Support.Middleware;
using GlowMe.ViewModels.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Admin;

[Route("admin")]
public class AdminAuthController : BaseController
{
    private const int Max = 3;

    private const int Decay = 900;

    private readonly AppDbContext _db;
    private readonly RateLimiterStore _limiter;

    public AdminAuthController(AppDbContext db, RateLimiterStore limiter)
    {
        _db = db;
        _limiter = limiter;
    }

    [HttpGet("login", Name = "admin.login")]
    [GlowGuest]
    public IActionResult Show() =>
        RedirectToAction("Show", "Auth", new { role = "admin" });

    [HttpPost("login", Name = "admin.login.post")]
    [GlowGuest]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model, [FromQuery] string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Login.cshtml", model);
        }

        var email = model.Email.Trim().ToLowerInvariant();
        var key = "admin-login:" + email;

        if (_limiter.TooManyAttempts(key, Max))
        {
            return Fail(model, "locked", _limiter.AvailableIn(key));
        }

        var admin = _db.Admins.FirstOrDefault(a => a.Email.ToLower() == email);

        if (admin is null || !Verify(model.Password, admin.PasswordHash))
        {
            _limiter.Hit(key, Decay);

            if (_limiter.TooManyAttempts(key, Max))
            {
                return Fail(model, "locked", _limiter.AvailableIn(key));
            }

            return Fail(model, "wrong", null);
        }

        _limiter.Clear(key);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.AdminID.ToString()),
            new(ClaimTypes.Name, admin.FullName),
            new(ClaimTypes.Role, "admin"),
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal).GetAwaiter().GetResult();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Dashboard", "AdminDashboard");
    }

    private IActionResult Fail(LoginViewModel model, string type, object? extra)
    {
        model.Password = string.Empty;

        ViewData["authErrorType"] = type;
        ViewData["authErrorExtra"] = extra;

        return View("~/Views/Admin/Login.cshtml", model);
    }

    private static bool Verify(string plain, string hash)
    {
        try
        {
            var normalised = hash.StartsWith("$2y$", StringComparison.Ordinal) ? "$2a$" + hash[4..] : hash;
            return BCrypt.Net.BCrypt.Verify(plain, normalised);
        }
        catch
        {
            return false;
        }
    }
}