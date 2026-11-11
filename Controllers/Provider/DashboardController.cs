using System.Globalization;
using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Middleware;
using GlowMe.Support.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Provider;

[Route("provider")]
public class ProviderDashboardController : BaseController
{
    private readonly AppDbContext _db;
    private readonly MaintenanceService _maint;

    public ProviderDashboardController(AppDbContext db, MaintenanceService maint)
    {
        _db = db;
        _maint = maint;
    }

    [HttpGet("", Name = "provider.dashboard")]
    [GlowAuth("provider")]
    public IActionResult Dashboard()
    {
        _maint.Run();

        var me = CurrentProvider(_db)!;
        var hour = DateTime.Now.Hour;
        var greeting = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";

        var pending = _db.Bookings.Count(b => b.ProviderID == me.ProviderID && b.BookingStatus == "Pending");

        var tomorrow = DateTime.Today.AddDays(1);
        var tomorrowCount = _db.Bookings.Count(b =>
            b.ProviderID == me.ProviderID && b.BookingStatus == "Accepted" && b.BookingDate == tomorrow);

        var earnings = MonthNet(me.ProviderID, DateTime.Today);

        var requests = _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .Include(b => b.Payments)
            .Where(b => b.ProviderID == me.ProviderID && b.BookingStatus == "Pending")
            .OrderByDescending(b => b.CreatedAt)
            .Take(2)
            .ToList();

        var tomorrowRows = _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .Where(b => b.ProviderID == me.ProviderID && b.BookingStatus == "Accepted" && b.BookingDate == tomorrow)
            .OrderBy(b => b.StartTime)
            .ToList();

        var activity = _db.Notifications
            .Where(n => n.ProviderID == me.ProviderID)
            .OrderByDescending(n => n.CreatedAt)
            .Take(5)
            .ToList();

        var bookingIds = _db.Bookings.Where(b => b.ProviderID == me.ProviderID).Select(b => b.BookingID);
        var reviewCount = _db.Reviews.Count(r => bookingIds.Contains(r.BookingID) && r.ReviewStatus == "Visible");

        var chart = new DashboardChart();
        for (var i = 5; i >= 0; i--)
        {
            var m = DateTime.Today.AddMonths(-i);
            chart.Labels.Add(m.ToString("MMM", CultureInfo.InvariantCulture));
            chart.Net.Add(Math.Round(MonthNet(me.ProviderID, m), 2));
            chart.Bookings.Add(_db.Bookings.Count(b =>
                b.ProviderID == me.ProviderID && b.BookingStatus == "Completed"
                && b.BookingDate.Year == m.Year && b.BookingDate.Month == m.Month));
        }

        return View("~/Views/Provider/Dashboard.cshtml", new ProviderDashboardViewModel
        {
            Me = me,
            Greeting = greeting,
            Pending = pending,
            TomorrowCount = tomorrowCount,
            Earnings = earnings,
            Requests = requests,
            TomorrowRows = tomorrowRows,
            Activity = activity,
            ReviewCount = reviewCount,
            Chart = chart,
        });
    }

    private decimal MonthNet(int providerId, DateTime month)
    {
        var completedIds = _db.Bookings
            .Where(b => b.ProviderID == providerId && b.BookingStatus == "Completed")
            .Select(b => b.BookingID);

        return _db.Payments
            .Where(p => p.BookingID != null && completedIds.Contains(p.BookingID.Value)
                        && p.PaymentStatus == "Paid"
                        && p.PaidAt != null
                        && p.PaidAt.Value.Year == month.Year
                        && p.PaidAt.Value.Month == month.Month)
            .Sum(p => p.ProviderAmount ?? 0);
    }
}

public class DashboardChart
{
    public List<string> Labels { get; set; } = new();
    public List<decimal> Net { get; set; } = new();
    public List<int> Bookings { get; set; } = new();
}

public class ProviderDashboardViewModel
{
    public Models.ServiceProvider Me { get; set; } = null!;
    public string Greeting { get; set; } = string.Empty;
    public int Pending { get; set; }
    public int TomorrowCount { get; set; }
    public decimal Earnings { get; set; }
    public List<Booking> Requests { get; set; } = new();
    public List<Booking> TomorrowRows { get; set; } = new();
    public List<Models.Notification> Activity { get; set; } = new();
    public int ReviewCount { get; set; }
    public DashboardChart Chart { get; set; } = new();
}