using GlowMe.Data;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Admin;

[Route("admin")]
public class AdminProviderController : BaseController
{
    private readonly AppDbContext _db;

    public AdminProviderController(AppDbContext db) => _db = db;

    [HttpGet("providers", Name = "admin.providers")]
    [GlowAuth("admin")]
    public IActionResult Index([FromQuery] string? q, [FromQuery] string? status, [FromQuery] int page = 1)
    {
        q = (q ?? string.Empty).Trim();

        var query = _db.ServiceProviders.AsQueryable();

        if (q.Length > 0)
        {
            query = query.Where(p =>
                p.BusinessName.Contains(q) || p.FullName.Contains(q) || p.Email.Contains(q));
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(p => p.AccountStatus == status);
        }

        var providers = Paginated<Models.ServiceProvider>.From(query.OrderBy(p => p.BusinessName), page, 15);

        var activeFilters = new[] { q.Length > 0 ? q : null, status }
            .Count(v => !string.IsNullOrEmpty(v));

        return View("~/Views/Admin/Providers.cshtml", new AdminProvidersViewModel
        {
            Providers = providers,
            Q = q,
            Status = status,
            ActiveFilters = activeFilters,
        });
    }

    [HttpPost("providers/{provider:int}/toggle", Name = "admin.providers.toggle")]
    [GlowAuth("admin")]
    [ValidateAntiForgeryToken]
    public IActionResult Toggle(int provider)
    {
        var row = _db.ServiceProviders.Find(provider) ?? throw new HttpResponseException(404);

        row.AccountStatus = row.AccountStatus == "Suspended" ? "Active" : "Suspended";
        _db.SaveChanges();

        Flash(row.AccountStatus == "Suspended" ? "Provider suspended." : "Provider reactivated.");
        return RedirectToAction(nameof(Index));
    }
}

public class AdminProvidersViewModel
{
    public Paginated<Models.ServiceProvider> Providers { get; set; } = new();
    public string Q { get; set; } = string.Empty;
    public string? Status { get; set; }
    public int ActiveFilters { get; set; }
}