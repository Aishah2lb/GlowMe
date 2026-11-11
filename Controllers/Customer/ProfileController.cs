using System.ComponentModel.DataAnnotations;
using GlowMe.Data;
using GlowMe.Support.Middleware;
using GlowMe.Support.Rules;
using GlowMe.ViewModels.Concerns;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Customer;

public class CustomerProfileController : BaseController
{
    private readonly AppDbContext _db;

    public CustomerProfileController(AppDbContext db) => _db = db;

    [HttpGet("/profile", Name = "profile.edit")]
    [GlowAuth("customer")]
    public IActionResult Edit()
    {
        var me = CurrentCustomer(_db)!;

        var stats = new Dictionary<string, int>
        {
            ["total"] = _db.Bookings.Count(b => b.CustomerID == me.CustomerID),
            ["completed"] = _db.Bookings.Count(b => b.CustomerID == me.CustomerID && b.BookingStatus == "Completed"),
            ["upcoming"] = _db.Bookings.Count(b => b.CustomerID == me.CustomerID
                && (b.BookingStatus == "Pending" || b.BookingStatus == "Accepted")),
        };

        return View("~/Views/Customer/Profile.cshtml", new CustomerProfileViewModel
        {
            Me = me,
            Stats = stats,
            Form = new CustomerProfileForm
            {
                FullName = me.FullName,
                PhoneNumber = me.PhoneNumber,
                City = me.City,
                Address = me.Address,
                Latitude = me.Latitude,
                Longitude = me.Longitude,
            },
        });
    }

    [HttpPost("/profile", Name = "profile.update")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult Update(CustomerProfileForm form)
    {
        var me = CurrentCustomer(_db)!;

        form.PhoneNumber = System.Text.RegularExpressions.Regex
            .Replace(form.PhoneNumber ?? string.Empty, @"\s+", string.Empty);
        form.CustomerID = me.CustomerID;

        if (!ModelState.IsValid)
        {
            return Edit();
        }

        me.FullName = form.FullName;
        me.PhoneNumber = form.PhoneNumber;
        me.City = form.City;
        me.Address = form.Address;
        me.Latitude = form.Latitude;
        me.Longitude = form.Longitude;
        _db.SaveChanges();

        Flash("Your profile was updated.");
        return RedirectToAction(nameof(Edit));
    }

    [HttpPost("/profile/password", Name = "profile.password")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult Password(ChangePasswordForm form)
    {
        var me = CurrentCustomer(_db)!;

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

public class CustomerProfileForm
{
    public int CustomerID { get; set; }

    [Display(Name = "Full name")]
    [Required]
    [MaxLength(100)]
    [RegularExpression(@"^[\p{L}'\-]+(\s+[\p{L}'\-]+)+$",
        ErrorMessage = "Enter your full name as it appears on your ID.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Mobile number")]
    [Required]
    [GlowPhone]
    [UniquePhone(IgnoreType = "customer", IgnoreIdProperty = nameof(CustomerID),
        ErrorMessage = "This mobile number already has a GlowMe account.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Display(Name = "City")]
    [Required]
    [GlowCity(ErrorMessage = "Choose the city where you want to book.")]
    public string City { get; set; } = string.Empty;

    [Display(Name = "Address")]
    [MaxLength(250)]
    public string? Address { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }
}

public class ChangePasswordForm
{
    [Display(Name = "Current password")]
    [Required]
    [DataType(DataType.Password)]
    public string? current_password { get; set; }

    [Display(Name = "Password")]
    [Required(ErrorMessage = GlowRules.PasswordMessages.Weak)]
    [StringLength(GlowRules.PasswordMax, MinimumLength = GlowRules.PasswordMin,
        ErrorMessage = GlowRules.PasswordMessages.Weak)]
    [GlowPassword]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = GlowRules.PasswordMessages.Mismatch)]
    [DataType(DataType.Password)]
    public string Password_confirmation { get; set; } = string.Empty;
}

public class CustomerProfileViewModel
{
    public Models.Customer Me { get; set; } = null!;
    public Dictionary<string, int> Stats { get; set; } = new();
    public CustomerProfileForm Form { get; set; } = new();
}