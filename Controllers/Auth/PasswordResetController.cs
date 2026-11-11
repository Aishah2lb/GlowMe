using GlowMe.Data;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using GlowMe.Support.Services;
using GlowMe.ViewModels.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Auth;

public class PasswordResetController : BaseController
{
    private readonly AppDbContext _db;
    private readonly AccountLinks _links;

    public PasswordResetController(AppDbContext db, AccountLinks links)
    {
        _db = db;
        _links = links;
    }

    [HttpGet("/forgot-password", Name = "password.request")]
    [GlowGuest]
    public IActionResult Request()
    {
        return View("~/Views/Auth/Forgot.cshtml", new ForgotPasswordViewModel());
    }

    [HttpPost("/forgot-password", Name = "password.email")]
    [GlowGuest]
    [ValidateAntiForgeryToken]
    public IActionResult Email(ForgotPasswordViewModel model)
    {
        model.PrepareForValidation();

        if (!ModelState.IsValid)
        {
            return View("~/Views/Auth/Forgot.cshtml", model);
        }

        var email = model.Email.Trim().ToLowerInvariant();

        object? user = _db.Customers.FirstOrDefault(c => c.Email.ToLower() == email);
        var type = "customer";

        if (user is null)
        {
            user = _db.ServiceProviders.FirstOrDefault(p => p.Email.ToLower() == email);
            type = "provider";
        }

        var (id, address, hash) = user switch
        {
            Models.Customer c => (c.CustomerID, c.Email, c.PasswordHash),
            Models.ServiceProvider p => (p.ProviderID, p.Email, p.PasswordHash),
            _ => (0, string.Empty, string.Empty),
        };

        HttpContext.Session.SetString("reset_sent.email", address);
        HttpContext.Session.SetString("reset_sent.link", _links.Reset(type, id, hash));
        HttpContext.Session.SetString("reset_sent.expires", DateTime.Now.AddMinutes(60).ToString("HH:mm"));

        return RedirectToAction(nameof(Sent));
    }

    [HttpGet("/forgot-password/sent", Name = "password.sent")]
    [GlowGuest]
    public IActionResult Sent()
    {
        var email = HttpContext.Session.GetString("reset_sent.email");

        if (string.IsNullOrEmpty(email))
        {
            return RedirectToAction(nameof(Request));
        }

        return View("~/Views/Auth/ResetSent.cshtml", new ResetSentViewModel
        {
            Email = email,
            Link = HttpContext.Session.GetString("reset_sent.link") ?? string.Empty,
            Expires = HttpContext.Session.GetString("reset_sent.expires") ?? string.Empty,
        });
    }

    [HttpGet("/reset-password/{type}/{id:int}", Name = "password.reset")]
    [GlowGuest]
    public IActionResult Reset(string type, int id, [FromQuery] string? hash, [FromQuery] string? token)
    {
        if (token is null || _links.Unprotect(token) is null)
        {
            throw new HttpResponseException(403, "This password-reset link has expired. Please request a new one.");
        }

        var user = Find(type, id);

        if (user is null || !FixedTimeEquals(AccountLinks.Sha1(PasswordHashOf(user)), hash))
        {
            throw new HttpResponseException(403);
        }

        return View("~/Views/Auth/Reset.cshtml", new ResetPasswordViewModel
        {
            Token = token,
            FirstName = FirstNameOf(user),
        });
    }

    [HttpPost("/reset-password/{type}/{id:int}", Name = "password.update")]
    [GlowGuest]
    [ValidateAntiForgeryToken]
    public IActionResult Update(string type, int id, ResetPasswordViewModel model,
        [FromQuery] string? hash, [FromQuery] string? token)
    {
        if (token is null || _links.Unprotect(token) is null)
        {
            throw new HttpResponseException(403);
        }

        var user = Find(type, id);

        if (user is null || !FixedTimeEquals(AccountLinks.Sha1(PasswordHashOf(user)), hash))
        {
            throw new HttpResponseException(403);
        }

        if (!ModelState.IsValid)
        {
            return View("~/Views/Auth/Reset.cshtml", model);
        }

        var newHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

        switch (user)
        {
            case Models.Customer c: c.PasswordHash = newHash; break;
            case Models.ServiceProvider p: p.PasswordHash = newHash; break;
        }

        _db.SaveChanges();

        Flash("Your password was changed. Log in with your new password.");
        return RedirectToAction("Show", "Auth");
    }

    private object? Find(string type, int id) =>
        type == "customer"
            ? _db.Customers.Find(id)
            : _db.ServiceProviders.Find(id);

    private static string PasswordHashOf(object user) => user switch
    {
        Models.Customer c => c.PasswordHash,
        Models.ServiceProvider p => p.PasswordHash,
        _ => string.Empty,
    };

    private static string FirstNameOf(object user) => user switch
    {
        Models.Customer c => c.FirstName(),
        Models.ServiceProvider p => p.FirstName(),
        _ => string.Empty,
    };

    private static bool FixedTimeEquals(string a, string? b) =>
        b is not null && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a),
            System.Text.Encoding.UTF8.GetBytes(b));
}

public class ResetSentViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Expires { get; set; } = string.Empty;
}