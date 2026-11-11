using GlowMe.Controllers.Customer;
using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Provider;

[Route("provider")]
public class ProviderReviewController : BaseController
{
    private readonly AppDbContext _db;

    public ProviderReviewController(AppDbContext db) => _db = db;

    [HttpGet("reviews", Name = "provider.reviews")]
    [GlowAuth("provider")]
    public IActionResult Index([FromQuery] string? rating, [FromQuery] string sort = "new", [FromQuery] int page = 1)
    {
        var me = CurrentProvider(_db)!;

        var ids = _db.Bookings.Where(b => b.ProviderID == me.ProviderID).Select(b => b.BookingID);

        var query = _db.Reviews
            .Include(r => r.Booking).ThenInclude(b => b!.Customer)
            .Include(r => r.Booking).ThenInclude(b => b!.BookingServices).ThenInclude(bs => bs.Service)
            .Where(r => ids.Contains(r.BookingID) && r.ReviewStatus == "Visible");

        if (!string.IsNullOrEmpty(rating) && int.TryParse(rating, out var stars))
        {
            query = query.Where(r => r.Rating == stars);
        }

        query = sort switch
        {
            "high" => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            "low" => query.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt),
        };

        var reviews = Paginated<Review>.From(query, page, 10);

        var activeFilters = new[] { rating, sort != "new" ? sort : null }
            .Count(v => !string.IsNullOrEmpty(v));

        var count = _db.Reviews.Count(r => ids.Contains(r.BookingID) && r.ReviewStatus == "Visible");

        var breakdown = new Dictionary<int, RatingBar>();
        for (var i = 5; i >= 1; i--)
        {
            var c = _db.Reviews.Count(r => ids.Contains(r.BookingID) && r.ReviewStatus == "Visible" && r.Rating == i);
            breakdown[i] = new RatingBar(c, count > 0 ? (int)Math.Round(c / (double)count * 100) : 0);
        }

        return View("~/Views/Provider/Reviews.cshtml", new ProviderReviewsViewModel
        {
            Me = me,
            Reviews = reviews,
            Count = count,
            Breakdown = breakdown,
            Rating = rating,
            Sort = sort,
            ActiveFilters = activeFilters,
        });
    }
}

public class ProviderReviewsViewModel
{
    public Models.ServiceProvider Me { get; set; } = null!;
    public Paginated<Review> Reviews { get; set; } = new();
    public int Count { get; set; }
    public Dictionary<int, RatingBar> Breakdown { get; set; } = new();
    public string? Rating { get; set; }
    public string Sort { get; set; } = "new";
    public int ActiveFilters { get; set; }
}