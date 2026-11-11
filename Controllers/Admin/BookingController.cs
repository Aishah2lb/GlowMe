using GlowMe.Controllers.Customer;
using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Admin;

[Route("admin")]
public class AdminBookingController : BaseController
{
    private readonly AppDbContext _db;

    public AdminBookingController(AppDbContext db) => _db = db;

    [HttpGet("bookings", Name = "admin.bookings")]
    [GlowAuth("admin")]
    public IActionResult Index(
        [FromQuery] string? status,
        [FromQuery(Name = "provider")] int? providerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? q,
        [FromQuery] int page = 1)
    {
        q = (q ?? string.Empty).Trim();

        var query = _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Provider)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(b => b.BookingStatus == status);
        }

        if (providerId is not null)
        {
            query = query.Where(b => b.ProviderID == providerId);
        }

        if (from is not null)
        {
            query = query.Where(b => b.BookingDate >= from.Value.Date);
        }

        if (to is not null)
        {
            query = query.Where(b => b.BookingDate <= to.Value.Date);
        }

        if (q.Length > 0)
        {
            query = query.Where(b =>
                b.Customer!.FullName.Contains(q) || b.Provider!.BusinessName.Contains(q));
        }

        var bookings = Paginated<Booking>.From(query.OrderByDescending(b => b.CreatedAt), page, 20);

        var activeFilters = new object?[] { status, providerId, from, to, q.Length > 0 ? q : null }
            .Count(v => v is not null && !string.IsNullOrEmpty(v.ToString()));

        var providers = _db.ServiceProviders.OrderBy(p => p.BusinessName).ToList();

        return View("~/Views/Admin/Bookings.cshtml", new AdminBookingsViewModel
        {
            Bookings = bookings,
            Providers = providers,
            Status = status,
            ProviderId = providerId,
            From = from,
            To = to,
            Q = q,
            ActiveFilters = activeFilters,
        });
    }

    [HttpGet("bookings/{booking:int}", Name = "admin.bookings.show")]
    [GlowAuth("admin")]
    public IActionResult Show(int booking)
    {
        var b = Load(booking);
        return View("~/Views/Admin/BookingShow.cshtml", b);
    }

    [HttpGet("bookings/{booking:int}/invoice", Name = "admin.bookings.invoice")]
    [GlowAuth("admin")]
    public IActionResult Invoice(int booking)
    {
        var b = Load(booking);

        return View("~/Views/Print/Booking.cshtml", new PrintBookingViewModel
        {
            Booking = b,
            Mode = "invoice",
            Back = Url.Action(nameof(Index)) ?? "/admin/bookings",
        });
    }

    private Booking Load(int id) =>
        _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Provider)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .Include(b => b.Payments)
            .Include(b => b.Review)
            .FirstOrDefault(b => b.BookingID == id)
        ?? throw new HttpResponseException(404);
}

public class AdminBookingsViewModel
{
    public Paginated<Booking> Bookings { get; set; } = new();
    public List<Models.ServiceProvider> Providers { get; set; } = new();
    public string? Status { get; set; }
    public int? ProviderId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string Q { get; set; } = string.Empty;
    public int ActiveFilters { get; set; }
}