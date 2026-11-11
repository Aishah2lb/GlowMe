using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Admin;

[Route("admin")]
public class AdminPaymentController : BaseController
{
    private readonly AppDbContext _db;

    public AdminPaymentController(AppDbContext db) => _db = db;

    [HttpGet("payments", Name = "admin.payments")]
    [GlowAuth("admin")]
    public IActionResult Index([FromQuery] string? type, [FromQuery] string? status, [FromQuery] int page = 1)
    {
        var query = _db.Payments
            .Include(p => p.Booking).ThenInclude(b => b!.Customer)
            .Include(p => p.Booking).ThenInclude(b => b!.Provider)
            .Include(p => p.Subscription).ThenInclude(s => s!.Provider)
            .AsQueryable();

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(p => p.PaymentType == type);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(p => p.PaymentStatus == status);
        }

        var payments = Paginated<Payment>.From(query.OrderByDescending(p => p.PaymentID), page, 20);

        var activeFilters = new[] { type, status }.Count(v => !string.IsNullOrEmpty(v));

        return View("~/Views/Admin/Payments.cshtml", new AdminPaymentsViewModel
        {
            Payments = payments,
            Type = type,
            Status = status,
            ActiveFilters = activeFilters,
        });
    }
}

public class AdminPaymentsViewModel
{
    public Paginated<Payment> Payments { get; set; } = new();
    public string? Type { get; set; }
    public string? Status { get; set; }
    public int ActiveFilters { get; set; }
}