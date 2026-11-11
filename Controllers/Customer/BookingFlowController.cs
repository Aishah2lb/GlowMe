using System.Text.Json;
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

public class BookingFlowController : BaseController
{
    private readonly AppDbContext _db;
    private readonly SlotService _slots;
    private readonly Support.Services.BookingService _bookings;

    public BookingFlowController(AppDbContext db, SlotService slots, Support.Services.BookingService bookings)
    {
        _db = db;
        _slots = slots;
        _bookings = bookings;
    }

    private static string Key(Models.ServiceProvider p) => $"booking.{p.ProviderID}";

    private BookingDraft Draft(Models.ServiceProvider p)
    {
        var json = HttpContext.Session.GetString(Key(p));
        return json is null ? new BookingDraft() : JsonSerializer.Deserialize<BookingDraft>(json)!;
    }

    private void PutDraft(Models.ServiceProvider p, BookingDraft d) =>
        HttpContext.Session.SetString(Key(p), JsonSerializer.Serialize(d));

    private List<Service> SelectedServices(Models.ServiceProvider p, IEnumerable<int> ids) =>
        _db.Services
            .Where(s => s.ProviderID == p.ProviderID && s.IsActive && ids.Contains(s.ServiceID))
            .ToList();

    [HttpPost("/book/{provider:int}/services", Name = "book.services")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult Services(int provider, [FromForm] int[]? services)
    {
        var p = LoadProvider(provider);

        if (services is null || services.Length < 1 || services.Length > 5)
        {
            ModelState.AddModelError("services", "Choose at least one service.");
            return RedirectToAction("Show", "ProviderProfile", new { provider });
        }

        var chosen = SelectedServices(p, services);

        if (chosen.Sum(s => s.DurationMinutes) > 480)
        {
            ModelState.AddModelError("services",
                "These services are too long for one appointment. Book them on two days.");
            return RedirectToAction("Show", "ProviderProfile", new { provider });
        }

        var draft = Draft(p);
        draft.Services = chosen.Select(s => s.ServiceID).ToList();
        PutDraft(p, draft);

        return RedirectToAction(nameof(Date), new { provider });
    }

    [HttpGet("/book/{provider:int}/date", Name = "book.date")]
    [GlowAuth("customer")]
    public IActionResult Date(int provider, [FromQuery] string? month, [FromQuery] string? d)
    {
        var p = LoadProvider(provider);
        var draft = Draft(p);

        if (draft.Services.Count == 0)
        {
            return RedirectToAction("Show", "ProviderProfile", new { provider });
        }

        var services = SelectedServices(p, draft.Services);
        var duration = services.Sum(s => s.DurationMinutes);

        month ??= DateTime.Today.ToString("yyyy-MM");
        var parts = month.Split('-');
        var y = int.Parse(parts[0]);
        var m = int.Parse(parts[1]);

        var calendar = _slots.MonthCalendar(p, y, m, duration);

        var selected = d;
        if (string.IsNullOrEmpty(selected))
        {
            foreach (var (date, cell) in calendar)
            {
                if (cell.Status == "open") { selected = date; break; }
            }
        }

        var selectedDate = selected is not null ? DateTime.Parse(selected) : (DateTime?)null;

        var slots = selectedDate is not null && _slots.HasOpenWindows(p, selectedDate.Value)
            ? _slots.FreeSlots(p, selectedDate.Value, duration)
            : new List<string>();

        var closed = selectedDate is not null && !_slots.HasOpenWindows(p, selectedDate.Value);
        var nextDays = slots.Count == 0 ? _slots.NextFreeDays(p, duration) : new List<DateTime>();

        return View("~/Views/Customer/Book/Date.cshtml", new BookDateViewModel
        {
            Provider = p,
            Services = services,
            Duration = duration,
            Calendar = calendar,
            Month = month,
            Y = y,
            M = m,
            Selected = selected,
            Slots = slots,
            Closed = closed,
            NextDays = nextDays,
            Draft = draft,
        });
    }

    [HttpPost("/book/{provider:int}/date", Name = "book.date.store")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult StoreDate(int provider, [FromForm] DateTime? BookingDate, [FromForm] string? StartTime)
    {
        var p = LoadProvider(provider);
        var draft = Draft(p);
        var services = SelectedServices(p, draft.Services);
        var duration = services.Sum(s => s.DurationMinutes);

        if (BookingDate is null
            || BookingDate.Value.Date < DateTime.Today
            || BookingDate.Value.Date > DateTime.Today.AddDays(60))
        {
            ModelState.AddModelError("BookingDate", "Pick a date.");
            return Date(provider, null, null);
        }

        if (string.IsNullOrEmpty(StartTime))
        {
            ModelState.AddModelError("StartTime", "This time is no longer free. Pick another slot.");
            return Date(provider, null, null);
        }

        var free = _slots.FreeSlots(p, BookingDate.Value, duration);

        if (!free.Contains(StartTime))
        {
            ModelState.AddModelError("StartTime", "This time is no longer free. Pick another slot.");
            return Date(provider, null, null);
        }

        draft.Date = BookingDate.Value.Date;
        draft.Start = BookingDate.Value.Date.Add(TimeSpan.Parse(StartTime));
        PutDraft(p, draft);

        return RedirectToAction(nameof(Location), new { provider });
    }

    [HttpGet("/book/{provider:int}/location", Name = "book.location")]
    [GlowAuth("customer")]
    public IActionResult Location(int provider)
    {
        var p = LoadProvider(provider);
        var draft = Draft(p);

        if (draft.Start == default)
        {
            return RedirectToAction(nameof(Date), new { provider });
        }

        return View("~/Views/Customer/Book/Location.cshtml", new BookLocationPageViewModel
        {
            Provider = p,
            Draft = draft,
            Services = SelectedServices(p, draft.Services),
            Me = CurrentCustomer(_db)!,
            Form = new BookLocationViewModel(),
        });
    }

    [HttpPost("/book/{provider:int}/location", Name = "book.location.store")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult StoreLocation(int provider, BookLocationViewModel model)
    {
        var p = LoadProvider(provider);
        var draft = Draft(p);

        if (!ModelState.IsValid)
        {
            return Location(provider);
        }

        if (model.LocationType == "Customer home" && !p.OffersHomeService)
        {
            ModelState.AddModelError("LocationType", "This provider does not offer home visits.");
            return Location(provider);
        }

        var address = model.LocationType == "Provider studio"
            ? (!string.IsNullOrEmpty(p.Address) ? p.Address : p.City)
            : model.LocationAddress ?? string.Empty;

        draft.LocationType = model.LocationType;
        draft.Address = address;
        draft.Notes = model.Notes;
        PutDraft(p, draft);

        return RedirectToAction(nameof(Payment), new { provider });
    }

    [HttpGet("/book/{provider:int}/payment", Name = "book.payment")]
    [GlowAuth("customer")]
    public IActionResult Payment(int provider)
    {
        var p = LoadProvider(provider);
        var draft = Draft(p);

        if (string.IsNullOrEmpty(draft.LocationType))
        {
            return RedirectToAction(nameof(Location), new { provider });
        }

        var services = SelectedServices(p, draft.Services);
        var total = Math.Round(services.Sum(s => s.Price), 2);
        var deposit = Math.Round(total * (decimal)GlowMeOptions.DepositRate, 2);

        return View("~/Views/Customer/Book/Payment.cshtml", new BookPaymentViewModel
        {
            Provider = p,
            Draft = draft,
            Services = services,
            Total = total,
            Deposit = deposit,
        });
    }

    [HttpPost("/book/{provider:int}/payment", Name = "book.payment.store")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult StorePayment(int provider, [FromForm] string? PaymentOption, [FromForm] string? PaymentMethod)
    {
        var p = LoadProvider(provider);
        var draft = Draft(p);

        if (PaymentOption is not ("Deposit" or "Full"))
        {
            ModelState.AddModelError("PaymentOption", "Choose to pay a deposit or the full amount.");
            return Payment(provider);
        }

        if (PaymentMethod is not ("Mada" or "Credit card" or "Apple Pay" or "STC Pay"))
        {
            ModelState.AddModelError("PaymentMethod", "Choose a payment method.");
            return Payment(provider);
        }

        draft.PaymentOption = PaymentOption;
        draft.Method = PaymentMethod;
        PutDraft(p, draft);

        Booking booking;
        try
        {
            var me = CurrentCustomer(_db)!;
            booking = _bookings.Hold(me, p, draft);
        }
        catch (SlotTakenException e)
        {
            ModelState.AddModelError("StartTime", e.Message);
            return RedirectToAction(nameof(Date), new { provider });
        }

        return RedirectToAction(nameof(Card), new { booking = booking.BookingID });
    }

    [HttpGet("/book/booking/{booking:int}/card", Name = "book.card")]
    [GlowAuth("customer")]
    public IActionResult Card(int booking, [FromQuery] int? declined)
    {
        var b = LoadBooking(booking);
        AuthorizeBooking(b);
        _bookings.CancelExpiredHold(b);

        if (b.BookingStatus != "Pending" || PaidAlready(b))
        {
            if (PaidAlready(b))
            {
                return RedirectToAction("Sent", "CustomerBooking", new { booking });
            }

            Flash("Your 10-minute hold expired. Please choose your slot again.");
            return RedirectToAction("Show", "ProviderProfile", new { provider = b.ProviderID });
        }

        var payment = b.Payments.FirstOrDefault(p => p.PaymentType is "Deposit" or "Full payment");
        var secondsLeft = _bookings.HoldSecondsLeft(b);

        var declineMessage = declined is not null
            ? HttpContext.Session.GetString("declineMessage") ?? "Declined — code 51, insufficient funds."
            : null;

        ViewData["payment"] = payment;
        ViewData["secondsLeft"] = secondsLeft;
        ViewData["declined"] = declineMessage;
        ViewData["booking"] = b;

        return View("~/Views/Customer/Book/Card.cshtml", new BookCardViewModel
        {
            IsWallet = payment is not null && payment.PaymentMethod is "Apple Pay" or "STC Pay",
        });
    }

    [HttpPost("/book/booking/{booking:int}/card", Name = "book.pay")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult Pay(int booking, BookCardViewModel model)
    {
        var b = LoadBooking(booking);
        AuthorizeBooking(b);
        _bookings.CancelExpiredHold(b);

        if (b.BookingStatus != "Pending")
        {
            Flash("Your 10-minute hold expired. Please choose your slot again.");
            return RedirectToAction("Show", "ProviderProfile", new { provider = b.ProviderID });
        }

        var payment = b.Payments.FirstOrDefault(p => p.PaymentType is "Deposit" or "Full payment");
        if (payment is not null && payment.PaymentMethod is "Apple Pay" or "STC Pay")
        {
            ModelState.Clear();
        }
        else if (!ModelState.IsValid)
        {
            return Card(booking, null);
        }

        var card = new CardDetails(model.card_name, model.card_number, model.card_expiry, model.card_cvc);
        var result = _bookings.Pay(b, card);

        if (!result.Success)
        {
            HttpContext.Session.SetString("declineMessage", result.Message ?? string.Empty);
            return RedirectToAction(nameof(Card), new { booking, declined = 1 });
        }

        HttpContext.Session.Remove($"booking.{b.ProviderID}");
        return RedirectToAction("Sent", "CustomerBooking", new { booking });
    }

    private static bool PaidAlready(Booking booking) =>
        booking.Payments.Any(p =>
            (p.PaymentType == "Deposit" || p.PaymentType == "Full payment") && p.PaymentStatus == "Paid");

    private void AuthorizeBooking(Booking booking)
    {
        if (booking.CustomerID != AuthId("customer"))
        {
            throw new HttpResponseException(403);
        }
    }

    private Models.ServiceProvider LoadProvider(int id)
    {
        var p = _db.ServiceProviders
            .Include(x => x.Subscriptions)
            .Include(x => x.Services).ThenInclude(s => s.Category)
            .FirstOrDefault(x => x.ProviderID == id);

        if (p is null || !p.IsBookable())
        {
            throw new HttpResponseException(404);
        }

        return p;
    }

    private Booking LoadBooking(int id) =>
        _db.Bookings
            .Include(b => b.Provider)
            .Include(b => b.Customer)
            .Include(b => b.Payments)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .FirstOrDefault(b => b.BookingID == id)
        ?? throw new HttpResponseException(404);
}

public class BookDateViewModel
{
    public Models.ServiceProvider Provider { get; set; } = null!;
    public List<Service> Services { get; set; } = new();
    public int Duration { get; set; }
    public Dictionary<string, CalendarCell> Calendar { get; set; } = new();
    public string Month { get; set; } = string.Empty;
    public int Y { get; set; }
    public int M { get; set; }
    public string? Selected { get; set; }
    public List<string> Slots { get; set; } = new();
    public bool Closed { get; set; }
    public List<DateTime> NextDays { get; set; } = new();
    public BookingDraft Draft { get; set; } = new();
}

public class BookLocationPageViewModel
{
    public Models.ServiceProvider Provider { get; set; } = null!;
    public BookingDraft Draft { get; set; } = new();
    public List<Service> Services { get; set; } = new();
    public Models.Customer Me { get; set; } = null!;
    public BookLocationViewModel Form { get; set; } = new();
}

public class BookPaymentViewModel
{
    public Models.ServiceProvider Provider { get; set; } = null!;
    public BookingDraft Draft { get; set; } = new();
    public List<Service> Services { get; set; } = new();
    public decimal Total { get; set; }
    public decimal Deposit { get; set; }
}