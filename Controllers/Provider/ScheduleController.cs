using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Middleware;
using GlowMe.Support.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Provider;

[Route("provider")]
public class ProviderScheduleController : BaseController
{
    private readonly AppDbContext _db;
    private readonly MaintenanceService _maint;

    public ProviderScheduleController(AppDbContext db, MaintenanceService maint)
    {
        _db = db;
        _maint = maint;
    }

    [HttpGet("schedule", Name = "provider.schedule")]
    [GlowAuth("provider")]
    public IActionResult Index([FromQuery] string? week)
    {
        _maint.Run();

        var me = CurrentProvider(_db)!;

        var start = week is not null
            ? StartOfWeek(DateTime.Parse(week))
            : StartOfWeek(DateTime.Today);

        var end = start.AddDays(6);

        var bookings = _db.Bookings
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .Include(b => b.Customer)
            .Where(b => b.ProviderID == me.ProviderID
                        && b.BookingDate >= start && b.BookingDate <= end
                        && (b.BookingStatus == "Pending" || b.BookingStatus == "Accepted" || b.BookingStatus == "Completed"))
            .ToList();

        var blocks = _db.Availabilities
            .Where(a => a.ProviderID == me.ProviderID && a.SlotStatus == "Blocked"
                        && a.AvailableDate >= start && a.AvailableDate <= end)
            .ToList();

        var open = _db.Availabilities
            .Where(a => a.ProviderID == me.ProviderID && a.SlotStatus == "Open"
                        && a.AvailableDate >= start && a.AvailableDate <= end)
            .ToList();

        var hours = Enumerable.Range(8, 13).ToList();

        var days = new List<DateTime>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            days.Add(d);
        }

        var prev = start.AddDays(-7).ToString("yyyy-MM-dd");
        var next = start.AddDays(7).ToString("yyyy-MM-dd");

        var appts = bookings.Count;
        var hoursBooked = bookings.Sum(b => (b.EndTime - b.StartTime).TotalMinutes) / 60;

        return View("~/Views/Provider/Schedule.cshtml", new ProviderScheduleViewModel
        {
            Me = me,
            Start = start,
            End = end,
            Days = days,
            Hours = hours,
            Bookings = bookings,
            Blocks = blocks,
            Open = open,
            Prev = prev,
            Next = next,
            Appts = appts,
            HoursBooked = hoursBooked,
        });
    }

    private static DateTime StartOfWeek(DateTime d)
    {
        var diff = ((int)d.DayOfWeek + 6) % 7;
        return d.Date.AddDays(-diff);
    }
}

public class ProviderScheduleViewModel
{
    public Models.ServiceProvider Me { get; set; } = null!;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public List<DateTime> Days { get; set; } = new();
    public List<int> Hours { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
    public List<Availability> Blocks { get; set; } = new();
    public List<Availability> Open { get; set; } = new();
    public string Prev { get; set; } = string.Empty;
    public string Next { get; set; } = string.Empty;
    public int Appts { get; set; }
    public double HoursBooked { get; set; }
}