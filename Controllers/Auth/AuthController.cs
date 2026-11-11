using System.Security.Claims;
using GlowMe.Data;
using GlowMe.Support;
using GlowMe.Support.Middleware;
using GlowMe.ViewModels.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Auth;

public class AuthController : BaseController
{
    private const int Max = 3;

    private const int Decay = 900;

    private readonly AppDbContext _db;
    private readonly IPasswordHasher<object> _hasher;
    private readonly RateLimiterStore _limiter;

    public AuthController(AppDbContext db, IPasswordHasher<object> hasher, RateLimiterStore limiter)
    {
        _db = db;
        _hasher = hasher;
        _limiter = limiter;
    }

    [HttpGet("/login", Name = "login")]
    [GlowGuest]
    public IActionResult Show([FromQuery] string role = "customer")
    {
        return View("~/Views/Auth/Login.cshtml", new LoginViewModel { Role = role });
    }

    [HttpPost("/login")]
    [GlowGuest]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model, [FromQuery] string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Auth/Login.cshtml", model);
        }

        var email = (model.Email ?? string.Empty).Trim().ToLowerInvariant();
        var role = model.Role is "customer" or "provider" or "admin" ? model.Role : "customer";

        var key = $"login:{role}:{email}";

        if (_limiter.TooManyAttempts(key, Max))
        {
            return Fail(model, "locked", _limiter.AvailableIn(key), role);
        }

        object? user = role switch
        {
            "admin" => _db.Admins.FirstOrDefault(a => a.Email.ToLower() == email),
            "provider" => _db.ServiceProviders.FirstOrDefault(p => p.Email.ToLower() == email),
            _ => _db.Customers.FirstOrDefault(c => c.Email.ToLower() == email),
        };

        var hash = user switch
        {
            Models.Admin a => a.PasswordHash,
            Models.ServiceProvider p => p.PasswordHash,
            Models.Customer c => c.PasswordHash,
            _ => null,
        };

        var ok = user is not null && hash is not null && CheckPassword(model.Password, hash);

        if (!ok)
        {
            _limiter.Hit(key, Decay);

            if (_limiter.TooManyAttempts(key, Max))
            {
                return Fail(model, "locked", _limiter.AvailableIn(key), role);
            }

            return Fail(model, "wrong", null, role);
        }

        if (role != "admin")
        {
            var status = user switch
            {
                Models.ServiceProvider p => p.AccountStatus,
                Models.Customer c => c.AccountStatus,
                _ => null,
            };

            if (status == "Suspended")
            {
                return Fail(model, "suspended", null, role);
            }

            if (status == "Pending")
            {
                return Fail(model, "pending", email, role);
            }
        }

        _limiter.Clear(key);

        var (id, name) = user switch
        {
            Models.Admin a => (a.AdminID, a.FullName),
            Models.ServiceProvider p => (p.ProviderID, p.FullName),
            Models.Customer c => (c.CustomerID, c.FullName),
            _ => (0, string.Empty),
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role),
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = model.Remember }).GetAwaiter().GetResult();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return role switch
        {
            "admin" => RedirectToAction("Dashboard", "AdminDashboard"),
            "provider" => RedirectToAction("Dashboard", "ProviderDashboard"),
            _ => RedirectToAction("Index", "Home"),
        };
    }

    private IActionResult Fail(LoginViewModel model, string type, object? extra, string role)
    {
        model.Password = string.Empty;

        TempData["authError.type"] = type;
        TempData["authError.extra"] = extra?.ToString();
        TempData["authError.role"] = role;

        ViewData["authErrorType"] = type;
        ViewData["authErrorExtra"] = extra;
        ViewData["authErrorRole"] = role;

        return View("~/Views/Auth/Login.cshtml", model);
    }

    [HttpPost("/logout", Name = "logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).GetAwaiter().GetResult();
        HttpContext.Session.Clear();

        return RedirectToAction("Show", "Auth");
    }

    private bool CheckPassword(string plain, string hash)
    {
        try
        {
            var normalised = hash.StartsWith("$2y$", StringComparison.Ordinal)
                ? "$2a$" + hash[4..]
                : hash;

            return BCrypt.Net.BCrypt.Verify(plain, normalised);
        }
        catch
        {
            return false;
        }
    }
}