using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Customer;

public class FavouriteController : BaseController
{
    private readonly AppDbContext _db;

    public FavouriteController(AppDbContext db) => _db = db;

    [HttpGet("/favourites", Name = "favourites.index")]
    [GlowAuth("customer")]
    public IActionResult Index()
    {
        var me = CurrentCustomer(_db)!;

        var providers = _db.Favorites
            .Where(f => f.CustomerID == me.CustomerID)
            .Include(f => f.Provider).ThenInclude(p => p!.Subscriptions)
            .Include(f => f.Provider).ThenInclude(p => p!.Portfolio)
            .Include(f => f.Provider).ThenInclude(p => p!.Services).ThenInclude(s => s.Category)
            .Select(f => f.Provider!)
            .ToList()
            .Where(p => p.IsBookable())
            .ToList();

        return View("~/Views/Customer/Favourites.cshtml", new FavouritesViewModel
        {
            Me = me,
            Providers = providers,
        });
    }

    [HttpPost("/providers/{provider:int}/favourite", Name = "favourites.toggle")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult Toggle(int provider)
    {
        var me = CurrentCustomer(_db)!;

        var existing = _db.Favorites
            .FirstOrDefault(f => f.CustomerID == me.CustomerID && f.ProviderID == provider);

        bool saved;

        if (existing is not null)
        {
            _db.Favorites.Remove(existing);
            saved = false;
        }
        else
        {
            _db.Favorites.Add(new Favorite
            {
                CustomerID = me.CustomerID,
                ProviderID = provider,
                AddedAt = DateTime.Now,
            });
            saved = true;
        }

        _db.SaveChanges();

        TempData["favSaved"] = saved;
        return Redirect(Request.Headers.Referer.ToString() is { Length: > 0 } r ? r : "/favourites");
    }
}

public class FavouritesViewModel
{
    public Models.Customer Me { get; set; } = null!;
    public List<Models.ServiceProvider> Providers { get; set; } = new();
}