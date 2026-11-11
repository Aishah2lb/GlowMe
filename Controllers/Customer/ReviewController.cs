using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using GlowMe.Support.Services;
using GlowMe.ViewModels.Customer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Customer;

public class CustomerReviewController : BaseController
{
    private readonly AppDbContext _db;
    private readonly RatingService _ratings;
    private readonly NotificationService _notify;

    public CustomerReviewController(AppDbContext db, RatingService ratings, NotificationService notify)
    {
        _db = db;
        _ratings = ratings;
        _notify = notify;
    }

    [HttpGet("/bookings/{booking:int}/review", Name = "reviews.create")]
    [GlowAuth("customer")]
    public IActionResult Create(int booking)
    {
        var b = Load(booking);
        Guard(b);

        ViewData["booking"] = b;
        return View("~/Views/Customer/Review.cshtml", new ReviewViewModel());
    }

    [HttpPost("/bookings/{booking:int}/review", Name = "reviews.store")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult Store(int booking, ReviewViewModel model)
    {
        var b = Load(booking);
        Guard(b);

        if (!ModelState.IsValid)
        {
            ViewData["booking"] = b;
            return View("~/Views/Customer/Review.cshtml", model);
        }

        using var tx = _db.Database.BeginTransaction();

        _db.Reviews.Add(new Review
        {
            BookingID = b.BookingID,
            Rating = model.Rating!.Value,
            Comment = model.Comment,
            ReviewStatus = "Visible",
            CreatedAt = DateTime.Now,
        });
        _db.SaveChanges();

        _ratings.Recalculate(b.ProviderID);

        _notify.ToProvider(b.ProviderID, "Review",
            $"{b.Customer!.FullName} rated you {model.Rating} stars for booking {b.Ref()}.");

        tx.Commit();

        Flash("Thank you! Your review is now visible on the profile.");
        return RedirectToAction("Show", "CustomerBooking", new { booking = b.BookingID });
    }

    private Booking Load(int id) =>
        _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Provider)
            .Include(b => b.Review)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .FirstOrDefault(b => b.BookingID == id)
        ?? throw new HttpResponseException(404);

    private void Guard(Booking booking)
    {
        if (booking.CustomerID != AuthId("customer"))
        {
            throw new HttpResponseException(403);
        }

        if (booking.BookingStatus != "Completed")
        {
            throw new HttpResponseException(422);
        }

        if (booking.Review is not null)
        {
            throw new HttpResponseException(422);
        }
    }
}