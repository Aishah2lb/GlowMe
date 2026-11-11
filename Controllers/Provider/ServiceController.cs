using System.ComponentModel.DataAnnotations;
using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Provider;

[Route("provider")]
public class ProviderServiceController : BaseController
{
    private readonly AppDbContext _db;

    public ProviderServiceController(AppDbContext db) => _db = db;

    [HttpGet("services", Name = "provider.services")]
    [GlowAuth("provider")]
    public IActionResult Index()
    {
        var me = CurrentProvider(_db)!;

        var services = _db.Services
            .Include(s => s.Category)
            .Where(s => s.ProviderID == me.ProviderID)
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.ServiceID)
            .ToList();

        var categories = _db.Categories.OrderBy(c => c.CategoryID).ToList();

        return View("~/Views/Provider/Services/Index.cshtml", new ProviderServicesViewModel
        {
            Me = me,
            Services = services,
            Categories = categories,
        });
    }

    [HttpPost("services", Name = "provider.services.store")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Store(ServiceForm form)
    {
        var me = CurrentProvider(_db)!;

        ValidateService(form, me.ProviderID, null);

        if (!ModelState.IsValid)
        {
            return Index();
        }

        _db.Services.Add(new Service
        {
            ProviderID = me.ProviderID,
            CategoryID = form.CategoryID!.Value,
            ServiceName = form.ServiceName,
            Description = form.Description,
            Price = form.Price!.Value,
            DurationMinutes = form.DurationMinutes!.Value,
            IsActive = true,
        });
        _db.SaveChanges();

        Flash("Service added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("services/{service:int}", Name = "provider.services.update")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Update(int service, ServiceForm form)
    {
        var s = _db.Services.Find(service) ?? throw new HttpResponseException(404);
        Mine(s);

        ValidateService(form, s.ProviderID, s.ServiceID);

        if (!ModelState.IsValid)
        {
            return Index();
        }

        s.ServiceName = form.ServiceName;
        s.CategoryID = form.CategoryID!.Value;
        s.Description = form.Description;
        s.Price = form.Price!.Value;
        s.DurationMinutes = form.DurationMinutes!.Value;
        _db.SaveChanges();

        Flash("Service updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("services/{service:int}/toggle", Name = "provider.services.toggle")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Toggle(int service)
    {
        var s = _db.Services.Find(service) ?? throw new HttpResponseException(404);
        Mine(s);

        s.IsActive = !s.IsActive;
        _db.SaveChanges();

        Flash(s.IsActive ? "Service activated." : "Service deactivated.");
        return RedirectToAction(nameof(Index));
    }

    private void ValidateService(ServiceForm form, int providerId, int? ignore)
    {
        var clash = _db.Services.Any(s =>
            s.ProviderID == providerId
            && s.ServiceName == form.ServiceName
            && (ignore == null || s.ServiceID != ignore));

        if (clash)
        {
            ModelState.AddModelError(nameof(form.ServiceName), "You already have a service with this name.");
        }

        if (form.CategoryID is null || !_db.Categories.Any(c => c.CategoryID == form.CategoryID))
        {
            ModelState.AddModelError(nameof(form.CategoryID), "Choose a category.");
        }

        if (form.DurationMinutes is not null && form.DurationMinutes % 15 != 0)
        {
            ModelState.AddModelError(nameof(form.DurationMinutes), "Choose a duration in steps of 15 minutes.");
        }
    }

    private void Mine(Service s)
    {
        if (s.ProviderID != AuthId("provider"))
        {
            throw new HttpResponseException(403);
        }
    }
}

public class ServiceForm
{
    [Display(Name = "Service name")]
    [Required]
    [MaxLength(100)]
    public string ServiceName { get; set; } = string.Empty;

    [Display(Name = "Category")]
    [Required]
    public int? CategoryID { get; set; }

    [Display(Name = "Description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Price")]
    [Required]
    [Range(10, 10000, ErrorMessage = "Enter a price between SAR 10 and SAR 10,000.")]
    public decimal? Price { get; set; }

    [Display(Name = "Duration")]
    [Required]
    [Range(15, 480, ErrorMessage = "Choose a duration in steps of 15 minutes.")]
    public int? DurationMinutes { get; set; }
}

public class ProviderServicesViewModel
{
    public Models.ServiceProvider Me { get; set; } = null!;
    public List<Service> Services { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
}