using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Customer;

public class HomeController : BaseController
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    [HttpGet("/home", Name = "home")]
    [GlowAuth("customer")]
    public IActionResult Index()
    {
        var me = CurrentCustomer(_db)!;
        var city = me.City;
        var hour = DateTime.Now.Hour;
        var greeting = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";

        var bookableInCity = Models.ServiceProvider.Bookable(_db.ServiceProviders)
            .Where(p => p.City == city)
            .Select(p => p.ProviderID)
            .ToList();

        var categories = _db.Categories
            .OrderBy(c => c.CategoryID)
            .ToList()
            .Select(cat =>
            {
                var svc = _db.Services.Where(s =>
                    s.CategoryID == cat.CategoryID && s.IsActive && bookableInCity.Contains(s.ProviderID));

                return new CategoryTile
                {
                    Category = cat,
                    ProviderCount = svc.Select(s => s.ProviderID).Distinct().Count(),
                    MinPrice = svc.Any() ? svc.Min(s => s.Price) : null,
                };
            })
            .ToList();

        var bookedServiceIds = _db.BookingServices
            .Join(_db.Bookings, bs => bs.BookingID, b => b.BookingID, (bs, b) => new { bs, b })
            .Where(x => x.b.CustomerID == me.CustomerID)
            .Select(x => x.bs.ServiceID)
            .ToList();

        var bookedCatIds = _db.Services
            .Where(s => bookedServiceIds.Contains(s.ServiceID))
            .Select(s => s.CategoryID)
            .Distinct()
            .ToList();

        var recQuery = Models.ServiceProvider.Bookable(_db.ServiceProviders).Where(p => p.City == city);

        if (bookedCatIds.Count > 0)
        {
            recQuery = recQuery.Where(p => p.Services.Any(s => s.IsActive && bookedCatIds.Contains(s.CategoryID)));
        }

        var recommended = recQuery
            .Include(p => p.Portfolio)
            .Include(p => p.Services).ThenInclude(s => s.Category)
            .OrderByDescending(p => p.AverageRating)
            .Take(4)
            .ToList();

        var recBasis = bookedCatIds.Count > 0 && recommended.Count > 0
            ? "In " + city + " · categories you booked before · best rated first"
            : "In " + city + " · best rated first";

        if (recommended.Count == 0)
        {
            recommended = Models.ServiceProvider.Bookable(_db.ServiceProviders)
                .Where(p => p.City == city)
                .Include(p => p.Portfolio)
                .Include(p => p.Services).ThenInclude(s => s.Category)
                .OrderByDescending(p => p.AverageRating)
                .Take(4)
                .ToList();
        }

        var counts = new Dictionary<string, int>
        {
            ["makeup"] = categories.FirstOrDefault(c => c.Category.CategoryName == "Makeup")?.ProviderCount ?? 0,
            ["hair"] = categories.FirstOrDefault(c => c.Category.CategoryName == "Hair Styling")?.ProviderCount ?? 0,
            ["nails"] = categories.FirstOrDefault(c => c.Category.CategoryName == "Nails")?.ProviderCount ?? 0,
        };

        return View("~/Views/Customer/Home.cshtml", new CustomerHomeViewModel
        {
            Me = me,
            City = city,
            Greeting = greeting,
            Categories = categories,
            Recommended = recommended,
            RecBasis = recBasis,
            Counts = counts,
        });
    }
}

public class CustomerHomeViewModel
{
    public Models.Customer Me { get; set; } = null!;
    public string City { get; set; } = string.Empty;
    public string Greeting { get; set; } = string.Empty;
    public List<CategoryTile> Categories { get; set; } = new();
    public List<Models.ServiceProvider> Recommended { get; set; } = new();
    public string RecBasis { get; set; } = string.Empty;
    public Dictionary<string, int> Counts { get; set; } = new();
}