using GlowMe.Data;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Admin;

[Route("admin")]
public class AdminReportController : BaseController
{
    private readonly AppDbContext _db;

    public AdminReportController(AppDbContext db) => _db = db;

    [HttpGet("reports", Name = "admin.reports")]
    [GlowAuth("admin")]
    public IActionResult Index([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        return View("~/Views/Admin/Reports.cshtml", BuildModel(from, to));
    }

    [HttpGet("reports/print", Name = "admin.reports.print")]
    [GlowAuth("admin")]
    public IActionResult Print([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var model = BuildModel(from, to);
        model.Back = Url.Action(nameof(Index), new { from = model.From, to = model.To })
                     ?? "/admin/reports";

        return View("~/Views/Print/Report.cshtml", model);
    }

    private AdminReportsViewModel BuildModel(DateTime? from, DateTime? to)
    {
        var fromDate = from?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var toDate = to?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            .AddMonths(1).AddDays(-1);

        var fromAt = fromDate;
        var toAt = toDate.AddDays(1).AddSeconds(-1);

        var customers = _db.Customers.Count();
        var providers = _db.ServiceProviders.Count(p => p.AccountStatus == "Active");

        var bookings = _db.Bookings.Count(b => b.CreatedAt >= fromAt && b.CreatedAt <= toAt);

        var completed = _db.Bookings.Count(b =>
            b.BookingStatus == "Completed" && b.UpdatedAt >= fromAt && b.UpdatedAt <= toAt);

        var visible = _db.Reviews
            .Where(r => r.ReviewStatus == "Visible")
            .Select(r => (decimal?)r.Rating)
            .ToList();
        var avgRating = visible.Count > 0 ? Math.Round(visible.Average()!.Value, 1) : 0m;

        var completedIds = _db.Bookings.Where(b => b.BookingStatus == "Completed").Select(b => b.BookingID);

        var commission = _db.Payments
            .Where(p => p.BookingID != null && completedIds.Contains(p.BookingID.Value)
                        && p.PaymentStatus == "Paid"
                        && p.PaidAt >= fromAt && p.PaidAt <= toAt)
            .Sum(p => p.CommissionAmount ?? 0);

        var subIncome = _db.Payments
            .Where(p => p.SubscriptionID != null && p.PaymentStatus == "Paid"
                        && p.PaidAt >= fromAt && p.PaidAt <= toAt)
            .Sum(p => p.Amount);

        var totalProfit = Math.Round(commission + subIncome, 2);

        var providerRows = _db.ServiceProviders
            .Select(p => new ProviderPerformanceRow
            {
                Provider = p,
                CompletedCount = p.Bookings.Count(b => b.BookingStatus == "Completed"),
            })
            .OrderByDescending(r => r.CompletedCount)
            .Take(10)
            .ToList();

        return new AdminReportsViewModel
        {
            From = fromDate,
            To = toDate,
            Customers = customers,
            Providers = providers,
            Bookings = bookings,
            Completed = completed,
            AvgRating = avgRating,
            Commission = commission,
            SubIncome = subIncome,
            TotalProfit = totalProfit,
            ProviderRows = providerRows,
        };
    }
}

public class ProviderPerformanceRow
{
    public Models.ServiceProvider Provider { get; set; } = null!;
    public int CompletedCount { get; set; }
}

public class AdminReportsViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int Customers { get; set; }
    public int Providers { get; set; }
    public int Bookings { get; set; }
    public int Completed { get; set; }
    public decimal AvgRating { get; set; }
    public decimal Commission { get; set; }
    public decimal SubIncome { get; set; }
    public decimal TotalProfit { get; set; }
    public List<ProviderPerformanceRow> ProviderRows { get; set; } = new();

    public string? Back { get; set; }
}