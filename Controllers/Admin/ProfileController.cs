using System.ComponentModel.DataAnnotations;
using GlowMe.Controllers.Customer;
using GlowMe.Data;
using GlowMe.Support.Middleware;
using GlowMe.Support.Rules;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Admin;

[Route("admin")]
public class AdminProfileController : BaseController
{
    private readonly AppDbContext _db;

    public AdminProfileController(AppDbContext db) => _db = db;

    [HttpGet("profile", Name = "admin.profile")]
    [GlowAuth("admin")]
    public IActionResult Edit()
    {
        var me = CurrentAdmin(_db)!;

        return View("~/Views/Admin/Profile.cshtml", new AdminProfileViewModel
        {
            Me = me,
            Form = new AdminProfileForm
            {
                AdminID = me.AdminID,
                FullName = me.FullName,
                Email = me.Email,
                AdminRole = me.AdminRole,
            },
        });
    }

    [HttpPost("profile", Name = "admin.profile.update")]
    [GlowAuth("admin")]
    [ValidateAntiForgeryToken]
    public IActionResult Update(AdminProfileForm form)
    {
        var me = CurrentAdmin(_db)!;

        form.Email = (form.Email ?? string.Empty).Trim().ToLowerInvariant();
        form.AdminID = me.AdminID;

        if (_db.Admins.Any(a => a.Email.ToLower() == form.Email && a.AdminID != me.AdminID))
        {
            ModelState.AddModelError(nameof(form.Email), "This email already has a GlowMe account.");
        }

        if (_db.Customers.Any(c => c.Email.ToLower() == form.Email)
            || _db.ServiceProviders.Any(p => p.Email.ToLower() == form.Email))
        {
            ModelState.AddModelError(nameof(form.Email), "This email already has a GlowMe account.");
        }

        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Profile.cshtml", new AdminProfileViewModel { Me = me, Form = form });
        }

        me.FullName = form.FullName;
        me.Email = form.Email;

        if (me.AdminRole == "Super admin")
        {
            me.AdminRole = form.AdminRole;
        }

        _db.SaveChanges();

        Flash("Your details were updated.");
        return RedirectToAction(nameof(Edit));
    }

    [HttpPost("profile/password", Name = "admin.profile.password")]
    [GlowAuth("admin")]
    [ValidateAntiForgeryToken]
    public IActionResult Password(ChangePasswordForm form)
    {
        var me = CurrentAdmin(_db)!;

        if (!BCrypt.Net.BCrypt.Verify(form.current_password ?? string.Empty, me.PasswordHash))
        {
            ModelState.AddModelError("current_password", "Your current password is incorrect.");
        }

        if (!ModelState.IsValid)
        {
            return Edit();
        }

        me.PasswordHash = BCrypt.Net.BCrypt.HashPassword(form.Password);
        _db.SaveChanges();

        Flash("Your password was changed.");
        return RedirectToAction(nameof(Edit));
    }
}

public class AdminProfileForm
{
    public int AdminID { get; set; }

    [Display(Name = "Full name")]
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Email address")]
    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Role")]
    [Required]
    [OneOf("Super admin", "Moderator", ErrorMessage = "Choose a valid role.")]
    public string AdminRole { get; set; } = "Moderator";
}

public class AdminProfileViewModel
{
    public Models.Admin Me { get; set; } = null!;
    public AdminProfileForm Form { get; set; } = new();
}