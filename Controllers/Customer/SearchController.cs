using GlowMe.Data;
using GlowMe.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Customer;

public class SearchController : BaseController
{
    private readonly AppDbContext _db;

    public SearchController(AppDbContext db) => _db = db;

    [HttpGet("/search", Name = "search")]
    public IActionResult Index(
        [FromQuery] string? city,
        [FromQuery] int[]? category,
        [FromQuery] string? service,
        [FromQuery] string? rating,
        [FromQuery] string? distance,
        [FromQuery] string? home,
        [FromQuery] string sort = "rating",
        [FromQuery] decimal? price_min = null,
        [FromQuery] decimal? price_max = null)
    {
        var me = CurrentCustomer(_db);
        var selectedCity = !string.IsNullOrEmpty(city) ? city : me?.City ?? Support.GlowMeOptions.Cities[0];

        var categoryIds = (category ?? Array.Empty<int>()).Where(v => v != 0).ToList();

        var serviceText = (service ?? string.Empty).Trim();

        var minRating = rating;
        var sortKey = sort;

        var query = Models.ServiceProvider.Bookable(_db.ServiceProviders)
            .Where(p => p.City == selectedCity);

        if (categoryIds.Count > 0)
        {
            query = query.Where(p => p.Services.Any(s => s.IsActive && categoryIds.Contains(s.CategoryID)));
        }

        if (serviceText.Length > 0)
        {
            query = query.Where(p =>
                p.BusinessName.Contains(serviceText)
                || p.FullName.Contains(serviceText)
                || p.Services.Any(s => s.IsActive && s.ServiceName.Contains(serviceText)));
        }

        if (!string.IsNullOrEmpty(minRating) && decimal.TryParse(minRating, out var min))
        {
            query = query.Where(p => p.AverageRating >= min);
        }

        if (home == "home")
        {
            query = query.Where(p => p.OffersHomeService);
        }
        else if (home == "studio")
        {
            query = query.Where(p => !p.OffersHomeService);
        }

        if (price_min is not null || price_max is not null)
        {
            query = query.Where(p => p.Services.Any(s => s.IsActive
                && (price_min == null || s.Price >= price_min)
                && (price_max == null || s.Price <= price_max)));
        }

        var providers = query
            .Include(p => p.Portfolio)
            .Include(p => p.Services).ThenInclude(s => s.Category)
            .ToList();

        var hasCoords = me is not null && me.Latitude is not null && me.Longitude is not null;

        var rows = providers
            .Select(p => new ProviderResult
            {
                Provider = p,
                DistanceKm = me is not null
                    ? p.DistanceFrom(
                        me.Latitude is not null ? (double)me.Latitude : null,
                        me.Longitude is not null ? (double)me.Longitude : null)
                    : null,
            })
            .ToList();

        var distanceApplied = hasCoords && !string.IsNullOrEmpty(distance) && distance != "any";

        if (distanceApplied && double.TryParse(distance, out var maxKm))
        {
            rows = rows.Where(r => r.DistanceKm is not null && r.DistanceKm <= maxKm).ToList();
        }

        rows = sortKey switch
        {
            "price" => rows.OrderBy(r => r.Provider.MinActivePrice() ?? decimal.MaxValue).ToList(),
            "distance" => rows.OrderBy(r => r.DistanceKm ?? double.MaxValue).ToList(),
            _ => rows.OrderByDescending(r => r.Provider.AverageRating ?? 0).ToList(),
        };

        var total = Models.ServiceProvider.Bookable(_db.ServiceProviders)
            .Where(p => p.City == selectedCity)
            .Where(p => categoryIds.Count == 0
                        || p.Services.Any(s => s.IsActive && categoryIds.Contains(s.CategoryID)))
            .Count();

        var categories = _db.Categories.OrderBy(c => c.CategoryID).ToList();

        var categoryName = categoryIds.Count == 1
            ? categories.FirstOrDefault(c => c.CategoryID == categoryIds[0])?.CategoryName
            : null;

        var sortLabels = new Dictionary<string, string>
        {
            ["rating"] = "Top rated",
            ["price"] = "Price, low to high",
            ["distance"] = "Distance",
        };

        return View("~/Views/Customer/Search.cshtml", new SearchViewModel
        {
            Me = me,
            City = selectedCity,
            Providers = rows,
            Total = total,
            Categories = categories,
            CategoryIds = categoryIds,
            CategoryName = categoryName,
            MinRating = minRating,
            Distance = distance,
            Home = home,
            Sort = sortKey,
            SortLabel = sortLabels.TryGetValue(sortKey, out var l) ? l : "Top rated",
            HasCoords = hasCoords,
            DistanceApplied = distanceApplied,
            PriceMin = price_min,
            PriceMax = price_max,
            ServiceText = serviceText,
        });
    }
}

public class ProviderResult
{
    public Models.ServiceProvider Provider { get; set; } = null!;
    public double? DistanceKm { get; set; }
}

public class SearchViewModel
{
    public Models.Customer? Me { get; set; }
    public string City { get; set; } = string.Empty;
    public List<ProviderResult> Providers { get; set; } = new();
    public int Total { get; set; }
    public List<Category> Categories { get; set; } = new();
    public List<int> CategoryIds { get; set; } = new();
    public string? CategoryName { get; set; }
    public string? MinRating { get; set; }
    public string? Distance { get; set; }
    public string? Home { get; set; }
    public string Sort { get; set; } = "rating";
    public string SortLabel { get; set; } = "Top rated";
    public bool HasCoords { get; set; }
    public bool DistanceApplied { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public string ServiceText { get; set; } = string.Empty;
}