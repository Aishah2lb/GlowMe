using System.Globalization;
using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers;

public class LandingController : BaseController
{
    private readonly AppDbContext _db;

    public LandingController(AppDbContext db) => _db = db;

    [HttpGet("/", Name = "landing")]
    public IActionResult Index()
    {
        var bookableIds = Models.ServiceProvider
            .Bookable(_db.ServiceProviders)
            .Select(p => p.ProviderID)
            .ToList();

        var categories = _db.Categories
            .OrderBy(c => c.CategoryID)
            .ToList()
            .Select(cat =>
            {
                var services = _db.Services
                    .Where(s => s.CategoryID == cat.CategoryID && s.IsActive && bookableIds.Contains(s.ProviderID));

                return new CategoryTile
                {
                    Category = cat,
                    ProviderCount = services.Select(s => s.ProviderID).Distinct().Count(),
                    MinPrice = services.Any() ? services.Min(s => s.Price) : null,
                };
            })
            .OrderByDescending(c => c.ProviderCount)
            .ToList();

        var quarterStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-2);
        var completed = _db.Bookings
            .Count(b => b.BookingStatus == "Completed" && b.BookingDate >= quarterStart);
        var quarterLabel = quarterStart.ToString("MMM", CultureInfo.InvariantCulture)
                           + " – " + DateTime.Today.ToString("MMM yyyy", CultureInfo.InvariantCulture);

        var activeProviders = bookableIds.Count;

        var visibleRatings = _db.Reviews
            .Where(r => r.ReviewStatus == "Visible")
            .Select(r => (decimal?)r.Rating)
            .ToList();
        var avgRating = visibleRatings.Count > 0 ? Math.Round(visibleRatings.Average()!.Value, 1) : 0m;

        var ranked = Models.ServiceProvider.Bookable(_db.ServiceProviders)
            .Include(p => p.Bookings)
            .Include(p => p.Portfolio)
            .Include(p => p.Services).ThenInclude(s => s.Category)
            .OrderByDescending(p => p.AverageRating)
            .ThenByDescending(p => p.CreatedAt)
            .ToList()
            .OrderByDescending(p => p.AverageRating ?? 0)
            .ThenByDescending(p => p.CompletedBookingsCount())
            .ToList();

        var topProviders = ranked.Take(3).ToList();

        var topIds = ranked.Take(6).Select(p => p.ProviderID).ToList();
        var gallery = _db.Portfolios
            .Where(ph => topIds.Contains(ph.ProviderID))
            .OrderByDescending(ph => ph.UploadedAt)
            .ToList()
            .OrderBy(ph => topIds.IndexOf(ph.ProviderID))
            .Take(6)
            .ToList();

        var fiveStar = _db.Reviews
            .Include(r => r.Booking).ThenInclude(b => b!.Customer)
            .Include(r => r.Booking).ThenInclude(b => b!.Provider).ThenInclude(p => p!.Portfolio)
            .Include(r => r.Booking).ThenInclude(b => b!.BookingServices).ThenInclude(bs => bs.Service)
            .Where(r => r.ReviewStatus == "Visible" && r.Rating >= 4)
            .OrderByDescending(r => r.Rating)
            .ThenByDescending(r => r.CreatedAt)
            .Take(8)
            .ToList()
            .Where(r => r.Booking is not null && r.Booking.Customer is not null)
            .ToList();

        var serviceNames = _db.Services
            .Where(s => s.IsActive && bookableIds.Contains(s.ProviderID))
            .OrderBy(s => s.ServiceName)
            .Select(s => s.ServiceName)
            .Distinct()
            .ToList();

        var cities = Models.ServiceProvider.Bookable(_db.ServiceProviders)
            .OrderBy(p => p.City)
            .Select(p => p.City)
            .Distinct()
            .ToList();

        return View(new LandingViewModel
        {
            Categories = categories,
            Completed = completed,
            QuarterLabel = quarterLabel,
            ActiveProviders = activeProviders,
            AvgRating = avgRating,
            Gallery = gallery,
            TopProviders = topProviders,
            FiveStar = fiveStar,
            ServiceNames = serviceNames,
            Cities = cities,
        });
    }

    [HttpGet("/search/suggest", Name = "search.suggest")]
    public IActionResult Suggest([FromQuery] string q = "")
    {
        q = (q ?? string.Empty).Trim();

        var bookableIds = Models.ServiceProvider
            .Bookable(_db.ServiceProviders)
            .Select(p => p.ProviderID)
            .ToList();

        var services = _db.Services
            .Include(s => s.Category)
            .Where(s => s.IsActive && bookableIds.Contains(s.ProviderID))
            .Where(s => q == string.Empty || s.ServiceName.Contains(q))
            .ToList()
            .GroupBy(s => s.ServiceName)
            .Select(group =>
            {
                var first = group.First();
                return new
                {
                    name = first.ServiceName,
                    from = "From SAR " + group.Min(s => s.Price).ToString("N0", CultureInfo.InvariantCulture),
                    image = Url.Content("~/img/categories/" + (first.Category?.Image() ?? "makeup.jpeg")),
                };
            })
            .Take(6)
            .ToList();

        return Json(services);
    }
}

public class LandingViewModel
{
    public List<CategoryTile> Categories { get; set; } = new();
    public int Completed { get; set; }
    public string QuarterLabel { get; set; } = string.Empty;
    public int ActiveProviders { get; set; }
    public decimal AvgRating { get; set; }
    public List<Portfolio> Gallery { get; set; } = new();
    public List<Models.ServiceProvider> TopProviders { get; set; } = new();
    public List<Review> FiveStar { get; set; } = new();
    public List<string> ServiceNames { get; set; } = new();
    public List<string> Cities { get; set; } = new();
}

public class CategoryTile
{
    public Category Category { get; set; } = null!;
    public int ProviderCount { get; set; }
    public decimal? MinPrice { get; set; }
}