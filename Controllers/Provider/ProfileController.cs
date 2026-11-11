using System.ComponentModel.DataAnnotations;
using GlowMe.Controllers.Customer;
using GlowMe.Data;
using GlowMe.Support;
using GlowMe.Support.Middleware;
using GlowMe.Support.Rules;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Provider;

[Route("provider")]
public class ProviderProfileController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProviderProfileController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet("profile", Name = "provider.profile")]
    [GlowAuth("provider")]
    public IActionResult Edit()
    {
        var me = CurrentProvider(_db)!;

        return View("~/Views/Provider/Profile.cshtml", new ProviderProfilePageViewModel
        {
            Me = me,
            Form = new ProviderProfileForm
            {
                ProviderID = me.ProviderID,
                FullName = me.FullName,
                BusinessName = me.BusinessName,
                PhoneNumber = me.PhoneNumber,
                City = me.City,
                Address = me.Address,
                Bio = me.Bio,
                OffersHomeService = me.OffersHomeService,
                Latitude = me.Latitude,
                Longitude = me.Longitude,
            },
        });
    }

    [HttpPost("profile", Name = "provider.profile.update")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Update(ProviderProfileForm form)
    {
        var me = CurrentProvider(_db)!;

        form.PhoneNumber = System.Text.RegularExpressions.Regex
            .Replace(form.PhoneNumber ?? string.Empty, @"\s+", string.Empty);
        form.ProviderID = me.ProviderID;

        if (!ModelState.IsValid)
        {
            return View("~/Views/Provider/Profile.cshtml",
                new ProviderProfilePageViewModel { Me = me, Form = form });
        }

        if (form.Photo is not null)
        {
            Img.Delete(me.ProfilePhotoURL, _env);
            me.ProfilePhotoURL = Img.Store(form.Photo, "profiles/provider", _env);
        }

        me.FullName = form.FullName;
        me.BusinessName = form.BusinessName;
        me.PhoneNumber = form.PhoneNumber;
        me.City = form.City;
        me.Address = form.Address;
        me.Bio = form.Bio;
        me.OffersHomeService = form.OffersHomeService ?? false;
        me.Latitude = form.Latitude;
        me.Longitude = form.Longitude;
        _db.SaveChanges();

        Flash("Your profile was updated.");
        return RedirectToAction(nameof(Edit));
    }

    [HttpPost("profile/password", Name = "provider.profile.password")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Password(ChangePasswordForm form)
    {
        var me = CurrentProvider(_db)!;

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

public class ProviderProfileForm
{
    public int ProviderID { get; set; }

    [Display(Name = "Full name")]
    [Required]
    [MaxLength(100)]
    [RegularExpression(@"^[\p{L}'\-]+(\s+[\p{L}'\-]+)+$",
        ErrorMessage = "Enter your full name as it appears on your ID.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Business name")]
    [Required]
    [MaxLength(100)]
    [UniqueBusinessName(IgnoreIdProperty = nameof(ProviderID),
        ErrorMessage = "Another provider already uses this business name.")]
    public string BusinessName { get; set; } = string.Empty;

    [Display(Name = "Mobile number")]
    [Required]
    [GlowPhone]
    [UniquePhone(IgnoreType = "provider", IgnoreIdProperty = nameof(ProviderID),
        ErrorMessage = "This mobile number already has a GlowMe account.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Display(Name = "City")]
    [Required]
    [GlowCity]
    public string City { get; set; } = string.Empty;

    [Display(Name = "Address")]
    [MaxLength(250)]
    public string? Address { get; set; }

    [Display(Name = "Bio")]
    [MaxLength(600)]
    public string? Bio { get; set; }

    [Display(Name = "Home visits")]
    [Required]
    public bool? OffersHomeService { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    [Display(Name = "Profile photo")]
    [AllowedFile("jpg", "jpeg", "png", "webp",
        ImageOnly = true,
        MaxKilobytes = 2048,
        MaxMessage = "Upload a photo up to 2 MB.")]
    public IFormFile? Photo { get; set; }
}

public class ProviderProfilePageViewModel
{
    public Models.ServiceProvider Me { get; set; } = null!;
    public ProviderProfileForm Form { get; set; } = new();
}