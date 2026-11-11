using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using GlowMe.Support.Services;
using GlowMe.Support.Services.Gateway;
using GlowMe.ViewModels.Customer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Customer;

public class CustomerBookingController : BaseController
{
    private readonly AppDbContext _db;
    private readonly Support.Services.BookingService _bookings;
    private readonly SlotService _slots;

    public CustomerBookingController(AppDbContext db, Support.Services.BookingService bookings, SlotService slots)
    {
        _db = db;
        _bookings = bookings;
        _slots = slots;
    }

    private void Mine(Booking booking)
    {
        if (booking.CustomerID != AuthId("customer"))
        {
            throw new HttpResponseException(403);
        }
    }

    [HttpGet("/bookings", Name = "bookings.index")]
    [GlowAuth("customer")]
    public IActionResult Index([FromQuery] string tab = "upcoming")
    {
        var me = CurrentCustomer(_db)!;
        ExpireHolds(me);

        var @base = _db.Bookings
            .Include(b => b.Provider)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .Include(b => b.Payments)
            .Include(b => b.Review)
            .Where(b => b.CustomerID == me.CustomerID);

        var filtered = tab switch
        {
            "past" => @base.Where(b => b.BookingStatus == "Completed"),
            "cancelled" => @base.Where(b => b.BookingStatus == "Cancelled" || b.BookingStatus == "Rejected"),
            _ => @base.Where(b => b.BookingStatus == "Pending" || b.BookingStatus == "Accepted"),
        };

        var bookings = tab is "upcoming" || tab is not ("past" or "cancelled")
            ? filtered.OrderBy(b => b.BookingDate).ThenBy(b => b.StartTime).ToList()
            : filtered.OrderByDescending(b => b.BookingDate).ThenByDescending(b => b.StartTime).ToList();

        var upcomingCount = _db.Bookings.Count(b => b.CustomerID == me.CustomerID
            && (b.BookingStatus == "Pending" || b.BookingStatus == "Accepted"));

        var completedCount = _db.Bookings.Count(b => b.CustomerID == me.CustomerID
            && b.BookingStatus == "Completed");

        return View("~/Views/Customer/Bookings/Index.cshtml", new BookingsIndexViewModel
        {
            Bookings = bookings,
            Tab = tab,
            UpcomingCount = upcomingCount,
            CompletedCount = completedCount,
        });
    }

    [HttpGet("/bookings/{booking:int}", Name = "bookings.show")]
    [GlowAuth("customer")]
    public IActionResult Show(int booking)
    {
        var b = Load(booking);
        Mine(b);

        _bookings.CancelExpiredHold(b);

        var deadline = b.CreatedAt.AddHours(GlowMeOptions.DecisionHours);

        return View("~/Views/Customer/Bookings/Show.cshtml", new BookingShowViewModel
        {
            Booking = b,
            Deadline = deadline,
        });
    }

    [HttpGet("/bookings/{booking:int}/sent", Name = "bookings.sent")]
    [GlowAuth("customer")]
    public IActionResult Sent(int booking)
    {
        var b = Load(booking);
        Mine(b);

        return View("~/Views/Customer/Bookings/Sent.cshtml", b);
    }

    [HttpPost("/bookings/{booking:int}/cancel", Name = "bookings.cancel")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult Cancel(int booking, CancelBookingViewModel model)
    {
        var b = Load(booking);
        Mine(b);

        if (b.BookingStatus is not ("Pending" or "Accepted"))
        {
            throw new HttpResponseException(422);
        }

        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Show), new { booking });
        }

        _bookings.CancelByCustomer(b, model.ResolvedReason());

        return RedirectToAction(nameof(Cancelled), new { booking });
    }

    [HttpGet("/bookings/{booking:int}/cancelled", Name = "bookings.cancelled")]
    [GlowAuth("customer")]
    public IActionResult Cancelled(int booking)
    {
        var b = Load(booking);
        Mine(b);

        var duration = (int)(b.EndTime - b.StartTime).TotalMinutes;
        var nextDays = _slots.NextFreeDays(b.Provider!, duration);

        return View("~/Views/Customer/Bookings/Cancelled.cshtml", new BookingCancelledViewModel
        {
            Booking = b,
            NextDays = nextDays,
        });
    }

    [HttpGet("/bookings/{booking:int}/reschedule", Name = "bookings.reschedule")]
    [GlowAuth("customer")]
    public IActionResult RescheduleForm(int booking, [FromQuery] string? month, [FromQuery] string? d)
    {
        var b = Load(booking);
        Mine(b);

        if (b.BookingStatus is not ("Pending" or "Accepted"))
        {
            throw new HttpResponseException(422);
        }

        var provider = b.Provider!;
        var duration = (int)(b.EndTime - b.StartTime).TotalMinutes;

        month ??= DateTime.Today.ToString("yyyy-MM");
        var parts = month.Split('-');
        var y = int.Parse(parts[0]);
        var m = int.Parse(parts[1]);

        var calendar = _slots.MonthCalendar(provider, y, m, duration);

        var selected = d;
        if (string.IsNullOrEmpty(selected))
        {
            foreach (var (date, cell) in calendar)
            {
                if (cell.Status == "open") { selected = date; break; }
            }
        }

        var selectedDate = selected is not null ? DateTime.Parse(selected) : (DateTime?)null;

        var slots = selectedDate is not null && _slots.HasOpenWindows(provider, selectedDate.Value)
            ? _slots.FreeSlots(provider, selectedDate.Value, duration, b.BookingID)
            : new List<string>();

        var closed = selectedDate is not null && !_slots.HasOpenWindows(provider, selectedDate.Value);

        return View("~/Views/Customer/Bookings/Reschedule.cshtml", new RescheduleFormViewModel
        {
            Booking = b,
            Provider = provider,
            Duration = duration,
            Calendar = calendar,
            Month = month,
            Y = y,
            M = m,
            Selected = selected,
            Slots = slots,
            Closed = closed,
        });
    }

    [HttpPost("/bookings/{booking:int}/reschedule", Name = "bookings.reschedule.store")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult Reschedule(int booking, RescheduleViewModel model)
    {
        var b = Load(booking);
        Mine(b);

        if (b.BookingStatus is not ("Pending" or "Accepted"))
        {
            throw new HttpResponseException(422);
        }

        foreach (var r in model.ValidateWindow())
        {
            ModelState.AddModelError(r.MemberNames.FirstOrDefault() ?? string.Empty, r.ErrorMessage!);
        }

        if (!ModelState.IsValid)
        {
            return RescheduleForm(booking, null, null);
        }

        var start = b.BookingDate.Date.Add(b.StartTime);

        if ((start - DateTime.Now).TotalHours < GlowMeOptions.CancelFreeHours)
        {
            ModelState.AddModelError("BookingDate",
                "You can change the date up to 24 hours before the appointment.");
            return RescheduleForm(booking, null, null);
        }

        var duration = (int)(b.EndTime - b.StartTime).TotalMinutes;
        var free = _slots.FreeSlots(b.Provider!, model.BookingDate!.Value, duration, b.BookingID);

        if (!free.Contains(model.StartTime))
        {
            ModelState.AddModelError("StartTime", "This time is no longer free. Pick another slot.");
            return RescheduleForm(booking, null, null);
        }

        try
        {
            _bookings.Reschedule(b, model.BookingDate.Value, TimeSpan.Parse(model.StartTime));
        }
        catch (SlotTakenException e)
        {
            ModelState.AddModelError("StartTime", e.Message);
            return RescheduleForm(booking, null, null);
        }

        Flash("Your new date was sent to the provider to accept.");
        return RedirectToAction(nameof(Show), new { booking });
    }

    [HttpGet("/bookings/{booking:int}/receipt", Name = "bookings.receipt")]
    [GlowAuth("customer")]
    public IActionResult Receipt(int booking)
    {
        var b = Load(booking);
        Mine(b);

        return View("~/Views/Print/Booking.cshtml", new PrintBookingViewModel
        {
            Booking = b,
            Mode = "receipt",
            Back = Url.Action(nameof(Show), new { booking }) ?? "/bookings",
        });
    }

    [HttpGet("/bookings/{booking:int}/pay-remaining", Name = "bookings.pay-remaining")]
    [GlowAuth("customer")]
    public IActionResult PayRemaining(int booking)
    {
        var b = Load(booking);
        Mine(b);

        if (b.RemainingAmount() <= 0.009m)
        {
            throw new HttpResponseException(404);
        }

        ViewData["booking"] = b;
        return View("~/Views/Customer/Bookings/PayRemaining.cshtml", new BookCardViewModel());
    }

    [HttpPost("/bookings/{booking:int}/pay-remaining", Name = "bookings.pay-remaining.store")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult PayRemainingStore(int booking, BookCardViewModel model)
    {
        var b = Load(booking);
        Mine(b);

        if (!ModelState.IsValid)
        {
            ViewData["booking"] = b;
            return View("~/Views/Customer/Bookings/PayRemaining.cshtml", model);
        }

        var card = new CardDetails(model.card_name, model.card_number, model.card_expiry, model.card_cvc);
        var result = _bookings.PayRemainingOnline(b, card, "Mada");

        if (!result.Success)
        {
            ModelState.AddModelError("card_number", result.Message ?? string.Empty);
            ViewData["booking"] = b;
            return View("~/Views/Customer/Bookings/PayRemaining.cshtml", model);
        }

        Flash("Your remaining balance was paid.");
        return RedirectToAction(nameof(Show), new { booking });
    }

    private void ExpireHolds(Models.Customer customer)
    {
        var holds = _db.Bookings
            .Include(b => b.Payments)
            .Where(b => b.CustomerID == customer.CustomerID && b.BookingStatus == "Pending")
            .ToList();

        foreach (var b in holds)
        {
            _bookings.CancelExpiredHold(b);
        }
    }

    private Booking Load(int id) =>
        _db.Bookings
            .Include(b => b.Provider).ThenInclude(p => p!.Availability)
            .Include(b => b.Customer)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .Include(b => b.Payments)
            .Include(b => b.Review)
            .FirstOrDefault(b => b.BookingID == id)
        ?? throw new HttpResponseException(404);
}

public class BookingsIndexViewModel
{
    public List<Booking> Bookings { get; set; } = new();
    public string Tab { get; set; } = "upcoming";
    public int UpcomingCount { get; set; }
    public int CompletedCount { get; set; }
}

public class BookingShowViewModel
{
    public Booking Booking { get; set; } = null!;
    public DateTime Deadline { get; set; }
}

public class BookingCancelledViewModel
{
    public Booking Booking { get; set; } = null!;
    public List<DateTime> NextDays { get; set; } = new();
}

public class RescheduleFormViewModel
{
    public Booking Booking { get; set; } = null!;
    public Models.ServiceProvider Provider { get; set; } = null!;
    public int Duration { get; set; }
    public Dictionary<string, CalendarCell> Calendar { get; set; } = new();
    public string Month { get; set; } = string.Empty;
    public int Y { get; set; }
    public int M { get; set; }
    public string? Selected { get; set; }
    public List<string> Slots { get; set; } = new();
    public bool Closed { get; set; }
}

public class PrintBookingViewModel
{
    public Booking Booking { get; set; } = null!;
    public string Mode { get; set; } = "receipt";
    public string Back { get; set; } = string.Empty;
}