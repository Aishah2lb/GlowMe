using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using GlowMe.Support.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Admin;

[Route("admin")]
public class AdminReviewController : BaseController
{
    private readonly AppDbContext _db;
    private readonly RatingService _ratings;

    public AdminReviewController(AppDbContext db, RatingService ratings)
    {
        _db = db;
        _ratings = ratings;
    }

    [HttpGet("reviews", Name = "admin.reviews")]
    [GlowAuth("admin")]
    public IActionResult Index([FromQuery] string? rating, [FromQuery] int page = 1)
    {
        var query = _db.Reviews
            .Include(r => r.Booking).ThenInclude(b => b!.Customer)
            .Include(r => r.Booking).ThenInclude(b => b!.Provider)
            .Include(r => r.Booking).ThenInclude(b => b!.BookingServices).ThenInclude(bs => bs.Service)
            .AsQueryable();

        if (!string.IsNullOrEmpty(rating) && int.TryParse(rating, out var stars))
        {
            query = query.Where(r => r.Rating == stars);
        }

        var reviews = Paginated<Review>.From(query.OrderByDescending(r => r.CreatedAt), page, 10);

        return View("~/Views/Admin/Reviews.cshtml", new AdminReviewsViewModel
        {
            Reviews = reviews,
            Rating = rating,
        });
    }

    [HttpPost("reviews/{review:int}/delete", Name = "admin.reviews.destroy")]
    [HttpDelete("reviews/{review:int}")]
    [GlowAuth("admin")]
    [ValidateAntiForgeryToken]
    public IActionResult Destroy(int review)
    {
        var row = _db.Reviews
            .Include(r => r.Booking)
            .FirstOrDefault(r => r.ReviewID == review)
            ?? throw new HttpResponseException(404);

        var providerId = row.Booking!.ProviderID;

        using var tx = _db.Database.BeginTransaction();

        _db.Reviews.Remove(row);
        _db.SaveChanges();

        _ratings.Recalculate(providerId);

        tx.Commit();

        Flash("Review deleted.");
        return RedirectToAction(nameof(Index));
    }
}

public class AdminReviewsViewModel
{
    public Paginated<Review> Reviews { get; set; } = new();
    public string? Rating { get; set; }
}