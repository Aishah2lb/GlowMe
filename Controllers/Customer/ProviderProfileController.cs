using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Customer;

public class ProviderProfileController : BaseController
{
    private readonly AppDbContext _db;

    public ProviderProfileController(AppDbContext db) => _db = db;

    [HttpGet("/providers/{provider:int}", Name = "providers.show")]
    public IActionResult Show(int provider, [FromQuery] string tab = "services", [FromQuery] int page = 1)
    {
        var p = _db.ServiceProviders
            .Include(x => x.Subscriptions)
            .Include(x => x.Portfolio)
            .Include(x => x.Services).ThenInclude(s => s.Category)
            .FirstOrDefault(x => x.ProviderID == provider);

        if (p is null || !p.IsBookable())
        {
            throw new HttpResponseException(404);
        }

        var me = CurrentCustomer(_db);

        var services = p.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.ServiceID)
            .ToList();

        var portfolio = p.Portfolio.OrderByDescending(ph => ph.UploadedAt).ToList();
        var cats = p.ActiveCategoryNames().ToList();

        var bookingIds = _db.Bookings
            .Where(b => b.ProviderID == p.ProviderID)
            .Select(b => b.BookingID)
            .ToList();

        var reviews = Paginated<Review>.From(
            _db.Reviews
                .Include(r => r.Booking).ThenInclude(b => b!.Customer)
                .Include(r => r.Booking).ThenInclude(b => b!.BookingServices).ThenInclude(bs => bs.Service)
                .Where(r => bookingIds.Contains(r.BookingID) && r.ReviewStatus == "Visible")
                .OrderByDescending(r => r.CreatedAt),
            page, 10);

        var visibleCount = _db.Reviews
            .Count(r => bookingIds.Contains(r.BookingID) && r.ReviewStatus == "Visible");

        var breakdown = new Dictionary<int, RatingBar>();
        for (var i = 5; i >= 1; i--)
        {
            var c = _db.Reviews.Count(r =>
                bookingIds.Contains(r.BookingID) && r.ReviewStatus == "Visible" && r.Rating == i);

            breakdown[i] = new RatingBar(c, visibleCount > 0 ? (int)Math.Round(c / (double)visibleCount * 100) : 0);
        }

        var completed = _db.Bookings.Count(b => b.ProviderID == p.ProviderID && b.BookingStatus == "Completed");

        var decided = _db.Bookings.Count(b => b.ProviderID == p.ProviderID
            && (b.BookingStatus == "Accepted" || b.BookingStatus == "Completed" || b.BookingStatus == "Rejected"));

        var acceptedN = _db.Bookings.Count(b => b.ProviderID == p.ProviderID
            && (b.BookingStatus == "Accepted" || b.BookingStatus == "Completed"));

        var acceptedPct = decided > 0 ? (int)Math.Round(acceptedN / (double)decided * 100) : 100;

        var isFav = me is not null
                    && _db.Favorites.Any(f => f.CustomerID == me.CustomerID && f.ProviderID == p.ProviderID);

        var dist = me is not null
            ? p.DistanceFrom(
                me.Latitude is not null ? (double)me.Latitude : null,
                me.Longitude is not null ? (double)me.Longitude : null)
            : null;

        return View("~/Views/Customer/Provider.cshtml", new ProviderProfileViewModel
        {
            Me = me,
            Provider = p,
            Tab = tab,
            Services = services,
            Portfolio = portfolio,
            Cats = cats,
            Reviews = reviews,
            Breakdown = breakdown,
            VisibleCount = visibleCount,
            Completed = completed,
            AcceptedPct = acceptedPct,
            IsFav = isFav,
            Dist = dist,
        });
    }
}

public record RatingBar(int Count, int Pct);

public class ProviderProfileViewModel
{
    public Models.Customer? Me { get; set; }
    public Models.ServiceProvider Provider { get; set; } = null!;
    public string Tab { get; set; } = "services";
    public List<Service> Services { get; set; } = new();
    public List<Portfolio> Portfolio { get; set; } = new();
    public List<string> Cats { get; set; } = new();
    public Paginated<Review> Reviews { get; set; } = new();
    public Dictionary<int, RatingBar> Breakdown { get; set; } = new();
    public int VisibleCount { get; set; }
    public int Completed { get; set; }
    public int AcceptedPct { get; set; }
    public bool IsFav { get; set; }
    public double? Dist { get; set; }
}