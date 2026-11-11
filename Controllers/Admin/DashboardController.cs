using System.Globalization;
using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Middleware;
using GlowMe.Support.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Admin;

[Route("admin")]
public class AdminDashboardController : BaseController
{
    private readonly AppDbContext _db;
    private readonly MaintenanceService _maint;

    public AdminDashboardController(AppDbContext db, MaintenanceService maint)
    {
        _db = db;
        _maint = maint;
    }

    [HttpGet("", Name = "admin.dashboard")]
    [GlowAuth("admin")]
    public IActionResult Dashboard()
    {
        _maint.Run();

        var customers = _db.Customers.Count();
        var providers = _db.ServiceProviders.Count();

        var now = DateTime.Today;

        var bookingsMonth = _db.Bookings
            .Count(b => b.CreatedAt.Month == now.Month && b.CreatedAt.Year == now.Year);

        var visible = _db.Reviews
            .Where(r => r.ReviewStatus == "Visible")
            .Select(r => (decimal?)r.Rating)
            .ToList();
        var avgRating = visible.Count > 0 ? Math.Round(visible.Average()!.Value, 1) : 0m;

        var completedIds = _db.Bookings.Where(b => b.BookingStatus == "Completed").Select(b => b.BookingID);

        var commissionMonth = _db.Payments
            .Where(p => p.BookingID != null && completedIds.Contains(p.BookingID.Value)
                        && p.PaymentStatus == "Paid"
                        && p.PaidAt != null && p.PaidAt.Value.Month == now.Month && p.PaidAt.Value.Year == now.Year)
            .Sum(p => p.CommissionAmount ?? 0);

        var subsMonth = _db.Payments
            .Where(p => p.SubscriptionID != null && p.PaymentStatus == "Paid"
                        && p.PaidAt != null && p.PaidAt.Value.Month == now.Month && p.PaidAt.Value.Year == now.Year)
            .Sum(p => p.Amount);

        var latest = _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Provider)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .OrderByDescending(b => b.CreatedAt)
            .Take(8)
            .ToList();

        var chart = new AdminChart();
        for (var i = 5; i >= 0; i--)
        {
            var m = now.AddMonths(-i);

            chart.Labels.Add(m.ToString("MMM", CultureInfo.InvariantCulture));

            chart.Bookings.Add(_db.Bookings
                .Count(b => b.CreatedAt.Year == m.Year && b.CreatedAt.Month == m.Month));

            chart.Commission.Add(Math.Round(_db.Payments
                .Where(p => p.BookingID != null && completedIds.Contains(p.BookingID.Value)
                            && p.PaymentStatus == "Paid"
                            && p.PaidAt != null && p.PaidAt.Value.Year == m.Year && p.PaidAt.Value.Month == m.Month)
                .Sum(p => p.CommissionAmount ?? 0), 2));

            chart.Subs.Add(Math.Round(_db.Payments
                .Where(p => p.SubscriptionID != null && p.PaymentStatus == "Paid"
                            && p.PaidAt != null && p.PaidAt.Value.Year == m.Year && p.PaidAt.Value.Month == m.Month)
                .Sum(p => p.Amount), 2));
        }

        return View("~/Views/Admin/Dashboard.cshtml", new AdminDashboardViewModel
        {
            Customers = customers,
            Providers = providers,
            BookingsMonth = bookingsMonth,
            AvgRating = avgRating,
            CommissionMonth = commissionMonth,
            SubsMonth = subsMonth,
            Latest = latest,
            Chart = chart,
        });
    }
}

public class AdminChart
{
    public List<string> Labels { get; set; } = new();
    public List<int> Bookings { get; set; } = new();
    public List<decimal> Commission { get; set; } = new();
    public List<decimal> Subs { get; set; } = new();
}

public class AdminDashboardViewModel
{
    public int Customers { get; set; }
    public int Providers { get; set; }
    public int BookingsMonth { get; set; }
    public decimal AvgRating { get; set; }
    public decimal CommissionMonth { get; set; }
    public decimal SubsMonth { get; set; }
    public List<Booking> Latest { get; set; } = new();
    public AdminChart Chart { get; set; } = new();
}