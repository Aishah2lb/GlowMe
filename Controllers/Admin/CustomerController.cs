using GlowMe.Data;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Admin;

[Route("admin")]
public class AdminCustomerController : BaseController
{
    private readonly AppDbContext _db;

    public AdminCustomerController(AppDbContext db) => _db = db;

    [HttpGet("customers", Name = "admin.customers")]
    [GlowAuth("admin")]
    public IActionResult Index([FromQuery] string? q, [FromQuery] string? status, [FromQuery] int page = 1)
    {
        q = (q ?? string.Empty).Trim();

        var query = _db.Customers.AsQueryable();

        if (q.Length > 0)
        {
            query = query.Where(c => c.FullName.Contains(q) || c.Email.Contains(q));
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(c => c.AccountStatus == status);
        }

        var customers = Paginated<Models.Customer>.From(query.OrderBy(c => c.FullName), page, 15);

        var activeFilters = new[] { q.Length > 0 ? q : null, status }
            .Count(v => !string.IsNullOrEmpty(v));

        return View("~/Views/Admin/Customers.cshtml", new AdminCustomersViewModel
        {
            Customers = customers,
            Q = q,
            Status = status,
            ActiveFilters = activeFilters,
        });
    }

    [HttpPost("customers/{customer:int}/toggle", Name = "admin.customers.toggle")]
    [GlowAuth("admin")]
    [ValidateAntiForgeryToken]
    public IActionResult Toggle(int customer)
    {
        var row = _db.Customers.Find(customer) ?? throw new HttpResponseException(404);

        row.AccountStatus = row.AccountStatus == "Suspended" ? "Active" : "Suspended";
        _db.SaveChanges();

        Flash(row.AccountStatus == "Suspended" ? "Customer suspended." : "Customer reactivated.");
        return RedirectToAction(nameof(Index));
    }
}

public class AdminCustomersViewModel
{
    public Paginated<Models.Customer> Customers { get; set; } = new();
    public string Q { get; set; } = string.Empty;
    public string? Status { get; set; }
    public int ActiveFilters { get; set; }
}