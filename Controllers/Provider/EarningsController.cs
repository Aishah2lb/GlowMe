using System.Globalization;
using System.Text;
using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Provider;

[Route("provider")]
public class ProviderEarningsController : BaseController
{
    private readonly AppDbContext _db;

    public ProviderEarningsController(AppDbContext db) => _db = db;

    [HttpGet("earnings", Name = "provider.earnings")]
    [GlowAuth("provider")]
    public IActionResult Index([FromQuery] string? month)
    {
        return View("~/Views/Provider/Earnings.cshtml", BuildModel(month));
    }

    [HttpGet("earnings/print", Name = "provider.earnings.print")]
    [GlowAuth("provider")]
    public IActionResult Printable([FromQuery] string? month)
    {
        var model = BuildModel(month);
        model.Back = Url.Action(nameof(Index), new { month = model.Month }) ?? "/provider/earnings";

        return View("~/Views/Print/Earnings.cshtml", model);
    }

    [HttpGet("earnings/export", Name = "provider.earnings.export")]
    [GlowAuth("provider")]
    public IActionResult Export([FromQuery] string? month)
    {
        var me = CurrentProvider(_db)!;

        month ??= DateTime.Today.ToString("yyyy-MM");
        var parts = month.Split('-');
        var y = int.Parse(parts[0]);
        var m = int.Parse(parts[1]);

        var rows = _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .Include(b => b.Payments)
            .Where(b => b.ProviderID == me.ProviderID && b.BookingStatus == "Completed"
                        && b.Payments.Any(p => p.PaymentStatus == "Paid"
                            && p.PaidAt != null && p.PaidAt.Value.Year == y && p.PaidAt.Value.Month == m))
            .OrderByDescending(b => b.BookingDate)
            .ToList();

        var csv = new StringBuilder("Date,Booking,Customer,Services,Total,Commission,Net\n");

        foreach (var b in rows)
        {
            var paid = b.Payments.Where(p => p.PaymentStatus == "Paid").ToList();

            csv.Append(string.Join(',', new[]
            {
                b.BookingDate.ToString("yyyy-MM-dd"),
                b.Ref(),
                "\"" + b.Customer!.FullName + "\"",
                "\"" + string.Join(" + ", b.Services.Select(s => s.ServiceName)) + "\"",
                paid.Sum(p => p.Amount).ToString("F2", CultureInfo.InvariantCulture),
                paid.Sum(p => p.CommissionAmount ?? 0).ToString("F2", CultureInfo.InvariantCulture),
                paid.Sum(p => p.ProviderAmount ?? 0).ToString("F2", CultureInfo.InvariantCulture),
            })).Append('\n');
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"glowme-earnings-{month}.csv");
    }

    private ProviderEarningsViewModel BuildModel(string? month)
    {
        var me = CurrentProvider(_db)!;

        month ??= DateTime.Today.ToString("yyyy-MM");
        var parts = month.Split('-');
        var y = int.Parse(parts[0]);
        var m = int.Parse(parts[1]);
        var period = new DateTime(y, m, 1);

        var completedIds = _db.Bookings
            .Where(b => b.ProviderID == me.ProviderID && b.BookingStatus == "Completed")
            .Select(b => b.BookingID);

        var payments = _db.Payments
            .Where(p => p.BookingID != null && completedIds.Contains(p.BookingID.Value)
                        && p.PaymentStatus == "Paid"
                        && p.PaidAt != null && p.PaidAt.Value.Year == y && p.PaidAt.Value.Month == m)
            .ToList();

        var value = payments.Sum(p => p.Amount);
        var commission = payments.Sum(p => p.CommissionAmount ?? 0);
        var net = payments.Sum(p => p.ProviderAmount ?? 0);

        var rows = _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .Include(b => b.Payments)
            .Where(b => b.ProviderID == me.ProviderID && b.BookingStatus == "Completed"
                        && b.Payments.Any(p => p.PaymentStatus == "Paid"
                            && p.PaidAt != null && p.PaidAt.Value.Year == y && p.PaidAt.Value.Month == m))
            .OrderByDescending(b => b.BookingDate)
            .ToList();

        var upcoming = _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .Include(b => b.Payments)
            .Where(b => b.ProviderID == me.ProviderID && b.BookingStatus == "Accepted"
                        && b.BookingDate >= DateTime.Today)
            .OrderBy(b => b.BookingDate)
            .ToList();

        return new ProviderEarningsViewModel
        {
            Me = me,
            Period = period,
            Month = month,
            Prev = period.AddMonths(-1).ToString("yyyy-MM"),
            Next = period.AddMonths(1).ToString("yyyy-MM"),
            Value = value,
            Commission = commission,
            Net = net,
            Rows = rows,
            Upcoming = upcoming,
            CompletedCount = rows.Count,
        };
    }
}

public class ProviderEarningsViewModel
{
    public Models.ServiceProvider Me { get; set; } = null!;
    public DateTime Period { get; set; }
    public string Month { get; set; } = string.Empty;
    public string Prev { get; set; } = string.Empty;
    public string Next { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal Commission { get; set; }
    public decimal Net { get; set; }
    public List<Booking> Rows { get; set; } = new();
    public List<Booking> Upcoming { get; set; } = new();
    public int CompletedCount { get; set; }

    public string? Back { get; set; }
}