using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using GlowMe.Support.Services;
using GlowMe.ViewModels.Provider;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Provider;

[Route("provider")]
public class ProviderRequestController : BaseController
{
    private readonly AppDbContext _db;
    private readonly Support.Services.BookingService _bookings;
    private readonly SlotService _slots;
    private readonly MaintenanceService _maint;

    public ProviderRequestController(AppDbContext db, Support.Services.BookingService bookings,
        SlotService slots, MaintenanceService maint)
    {
        _db = db;
        _bookings = bookings;
        _slots = slots;
        _maint = maint;
    }

    private void Mine(Booking b)
    {
        if (b.ProviderID != AuthId("provider"))
        {
            throw new HttpResponseException(403);
        }
    }

    [HttpGet("requests", Name = "provider.requests")]
    [GlowAuth("provider")]
    public IActionResult Index([FromQuery] string tab = "pending", [FromQuery] string? q = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        _maint.Run();

        var me = CurrentProvider(_db)!;
        q = (q ?? string.Empty).Trim();

        var @base = _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .Include(b => b.Payments)
            .Where(b => b.ProviderID == me.ProviderID);

        var query = tab switch
        {
            "accepted" => @base.Where(b => b.BookingStatus == "Accepted"),
            "completed" => @base.Where(b => b.BookingStatus == "Completed"),
            "rejected" => @base.Where(b => b.BookingStatus == "Rejected"),
            _ => @base.Where(b => b.BookingStatus == "Pending"),
        };

        if (q.Length > 0)
        {
            query = query.Where(b => b.Customer!.FullName.Contains(q));
        }

        if (from is not null)
        {
            query = query.Where(b => b.BookingDate >= from.Value.Date);
        }

        if (to is not null)
        {
            query = query.Where(b => b.BookingDate <= to.Value.Date);
        }

        var bookings = tab == "pending"
            ? query.OrderBy(b => b.CreatedAt).ToList()
            : query.OrderByDescending(b => b.CreatedAt).ToList();

        var counts = new Dictionary<string, int>
        {
            ["pending"] = _db.Bookings.Count(b => b.ProviderID == me.ProviderID && b.BookingStatus == "Pending"),
            ["accepted"] = _db.Bookings.Count(b => b.ProviderID == me.ProviderID && b.BookingStatus == "Accepted"),
            ["completed"] = _db.Bookings.Count(b => b.ProviderID == me.ProviderID && b.BookingStatus == "Completed"),
            ["rejected"] = _db.Bookings.Count(b => b.ProviderID == me.ProviderID && b.BookingStatus == "Rejected"),
        };

        var month = DateTime.Today.Month;

        var rejectedThisMonth = _db.Bookings.Count(b =>
            b.ProviderID == me.ProviderID && b.BookingStatus == "Rejected" && b.UpdatedAt.Month == month);

        var acceptedThisMonth = _db.Bookings.Count(b =>
            b.ProviderID == me.ProviderID
            && (b.BookingStatus == "Accepted" || b.BookingStatus == "Completed")
            && b.UpdatedAt.Month == month);

        return View("~/Views/Provider/Requests/Index.cshtml", new ProviderRequestsViewModel
        {
            Me = me,
            Tab = tab,
            Bookings = bookings,
            Counts = counts,
            RejectedThisMonth = rejectedThisMonth,
            AcceptedThisMonth = acceptedThisMonth,
            From = from,
            To = to,
            Q = q,
        });
    }

    [HttpGet("requests/{booking:int}", Name = "provider.requests.show")]
    [GlowAuth("provider")]
    public IActionResult Show(int booking)
    {
        var b = Load(booking);
        Mine(b);

        var me = CurrentProvider(_db)!;

        var conflict = FindConflict(b);
        var suggestions = conflict is not null ? SuggestTimes(b) : new List<NextSlot>();
        var noConflictNext = NextThatDay(b);

        var dayRows = _db.Bookings
            .Include(x => x.BookingServices).ThenInclude(bs => bs.Service)
            .Where(x => x.ProviderID == me.ProviderID && x.BookingDate == b.BookingDate.Date
                        && x.BookingID != b.BookingID && x.BookingStatus == "Accepted")
            .OrderBy(x => x.StartTime)
            .ToList();

        var withYou = _db.Bookings.Count(x =>
            x.ProviderID == me.ProviderID && x.CustomerID == b.CustomerID && x.BookingStatus == "Completed");

        var returning = withYou > 0;

        var window = GlowMeOptions.RejectWindowDays;
        var cut = DateTime.Today.AddDays(-window);

        var recentRejections = _db.Bookings.Count(x =>
            x.ProviderID == me.ProviderID && x.BookingStatus == "Rejected" && x.BookingDate >= cut);

        return View("~/Views/Provider/Requests/Show.cshtml", new ProviderRequestShowViewModel
        {
            Booking = b,
            Me = me,
            Conflict = conflict,
            Suggestions = suggestions,
            NoConflictNext = noConflictNext,
            DayRows = dayRows,
            WithYou = withYou,
            Returning = returning,
            RecentRejections = recentRejections,
            Form = new RejectViewModel(),
        });
    }

    [HttpPost("requests/{booking:int}/accept", Name = "provider.requests.accept")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Accept(int booking)
    {
        var b = Load(booking);
        Mine(b);

        if (b.BookingStatus != "Pending")
        {
            throw new HttpResponseException(422);
        }

        try
        {
            _bookings.Accept(b);
        }
        catch (SlotTakenException)
        {
            TempData["conflict"] = true;
            return RedirectToAction(nameof(Show), new { booking });
        }

        return RedirectToAction(nameof(Accepted), new { booking });
    }

    [HttpPost("requests/{booking:int}/reject", Name = "provider.requests.reject")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Reject(int booking, RejectViewModel model)
    {
        var b = Load(booking);
        Mine(b);

        if (b.BookingStatus != "Pending")
        {
            throw new HttpResponseException(422);
        }

        if (!ModelState.IsValid)
        {
            return Show(booking);
        }

        _bookings.Reject(b, model.ResolvedReason());

        return RedirectToAction(nameof(Rejected), new { booking });
    }

    [HttpPost("requests/{booking:int}/offer-time", Name = "provider.requests.offer")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult OfferTime(int booking)
    {
        var b = Load(booking);
        Mine(b);

        var times = SuggestTimes(b);

        var text = "Hi " + b.Customer!.FirstName() + ", I have a conflict at that time. I can offer: "
                   + string.Join(", ", times.Select(t => Fmt.RelativeDay(t.Date) + " " + t.Time))
                   + ". Would any of these work?";

        _db.Messages.Add(new Message
        {
            CustomerID = b.CustomerID,
            ProviderID = b.ProviderID,
            SenderType = "Provider",
            MessageText = text,
            IsRead = false,
            SentAt = DateTime.Now,
        });
        _db.SaveChanges();

        Flash("Your suggested times were sent to the customer.");
        return RedirectToAction("Index", "ProviderMessage", new { with = b.CustomerID });
    }

    [HttpGet("bookings/{booking:int}/accepted", Name = "provider.bookings.accepted")]
    [GlowAuth("provider")]
    public IActionResult Accepted(int booking)
    {
        var b = Load(booking);
        Mine(b);

        return View("~/Views/Provider/Bookings/Accepted.cshtml", b);
    }

    [HttpGet("bookings/{booking:int}/rejected", Name = "provider.bookings.rejected")]
    [GlowAuth("provider")]
    public IActionResult Rejected(int booking)
    {
        var b = Load(booking);
        Mine(b);

        var me = CurrentProvider(_db)!;
        var month = DateTime.Today.Month;

        var rejectedThisMonth = _db.Bookings.Count(x =>
            x.ProviderID == me.ProviderID && x.BookingStatus == "Rejected" && x.UpdatedAt.Month == month);

        var totalThisMonth = _db.Bookings.Count(x =>
            x.ProviderID == me.ProviderID
            && (x.BookingStatus == "Accepted" || x.BookingStatus == "Completed" || x.BookingStatus == "Rejected")
            && x.UpdatedAt.Month == month);

        return View("~/Views/Provider/Bookings/Rejected.cshtml", new ProviderRejectedViewModel
        {
            Booking = b,
            RejectedThisMonth = rejectedThisMonth,
            TotalThisMonth = totalThisMonth,
        });
    }

    [HttpGet("bookings/{booking:int}", Name = "provider.bookings.show")]
    [GlowAuth("provider")]
    public IActionResult BookingShow(int booking)
    {
        var b = Load(booking);
        Mine(b);

        var me = CurrentProvider(_db)!;

        var dayRows = _db.Bookings
            .Include(x => x.BookingServices).ThenInclude(bs => bs.Service)
            .Where(x => x.ProviderID == me.ProviderID && x.BookingDate == b.BookingDate.Date
                        && x.BookingID != b.BookingID && x.BookingStatus == "Accepted")
            .OrderBy(x => x.StartTime)
            .ToList();

        var endPassed = b.BookingDate.Date.Add(b.EndTime) < DateTime.Now;

        var withYou = _db.Bookings.Count(x =>
            x.ProviderID == me.ProviderID && x.CustomerID == b.CustomerID && x.BookingStatus == "Completed");

        return View("~/Views/Provider/Bookings/Show.cshtml", new ProviderBookingShowViewModel
        {
            Booking = b,
            Me = me,
            DayRows = dayRows,
            EndPassed = endPassed,
            WithYou = withYou,
            Returning = withYou > 0,
        });
    }

    [HttpPost("bookings/{booking:int}/complete", Name = "provider.bookings.complete")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Complete(int booking, CompleteViewModel model)
    {
        var b = Load(booking);
        Mine(b);

        if (b.BookingStatus != "Accepted")
        {
            throw new HttpResponseException(422);
        }

        var endPassed = b.BookingDate.Date.Add(b.EndTime) < DateTime.Now;

        if (!endPassed)
        {
            throw new HttpResponseException(422);
        }

        string? restMethod = null;

        if (b.PaymentOption == "Deposit" && b.RemainingAmount() > 0.009m)
        {
            restMethod = !string.IsNullOrEmpty(model.rest_method) ? model.rest_method : "Online";

            if (restMethod == "Online")
            {
            }
        }

        _bookings.Complete(b, restMethod == "Online" ? null : restMethod);

        return RedirectToAction(nameof(Completed), new { booking });
    }

    [HttpGet("bookings/{booking:int}/completed", Name = "provider.bookings.completed")]
    [GlowAuth("provider")]
    public IActionResult Completed(int booking)
    {
        var b = Load(booking);
        Mine(b);

        return View("~/Views/Provider/Bookings/Completed.cshtml", b);
    }

    private Booking? FindConflict(Booking booking)
    {
        if (booking.BookingStatus != "Pending")
        {
            return null;
        }

        return _db.Bookings
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .FirstOrDefault(b => b.ProviderID == AuthId("provider")
                && b.BookingStatus == "Accepted"
                && b.BookingDate == booking.BookingDate.Date
                && b.BookingID != booking.BookingID
                && b.StartTime < booking.EndTime
                && b.EndTime > booking.StartTime);
    }

    private Booking? NextThatDay(Booking booking) =>
        _db.Bookings
            .Where(b => b.ProviderID == AuthId("provider")
                        && b.BookingStatus == "Accepted"
                        && b.BookingDate == booking.BookingDate.Date
                        && b.StartTime >= booking.EndTime)
            .OrderBy(b => b.StartTime)
            .FirstOrDefault();

    private List<NextSlot> SuggestTimes(Booking booking)
    {
        var duration = (int)(booking.EndTime - booking.StartTime).TotalMinutes;
        var @out = new List<NextSlot>();

        for (var i = 0; i < 30 && @out.Count < 3; i++)
        {
            var date = DateTime.Today.AddDays(i);

            foreach (var slot in _slots.FreeSlots(booking.Provider!, date, duration, booking.BookingID))
            {
                @out.Add(new NextSlot(date, slot));
                if (@out.Count >= 3) { break; }
            }
        }

        return @out;
    }

    private Booking Load(int id) =>
        _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Provider).ThenInclude(p => p!.Availability)
            .Include(b => b.BookingServices).ThenInclude(bs => bs.Service)
            .Include(b => b.Payments)
            .FirstOrDefault(b => b.BookingID == id)
        ?? throw new HttpResponseException(404);
}

public class ProviderRequestsViewModel
{
    public Models.ServiceProvider Me { get; set; } = null!;
    public string Tab { get; set; } = "pending";
    public List<Booking> Bookings { get; set; } = new();
    public Dictionary<string, int> Counts { get; set; } = new();
    public int RejectedThisMonth { get; set; }
    public int AcceptedThisMonth { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string Q { get; set; } = string.Empty;
}

public class ProviderRequestShowViewModel
{
    public Booking Booking { get; set; } = null!;
    public Models.ServiceProvider Me { get; set; } = null!;
    public Booking? Conflict { get; set; }
    public List<NextSlot> Suggestions { get; set; } = new();
    public Booking? NoConflictNext { get; set; }
    public List<Booking> DayRows { get; set; } = new();
    public int WithYou { get; set; }
    public bool Returning { get; set; }
    public int RecentRejections { get; set; }
    public RejectViewModel Form { get; set; } = new();
}

public class ProviderRejectedViewModel
{
    public Booking Booking { get; set; } = null!;
    public int RejectedThisMonth { get; set; }
    public int TotalThisMonth { get; set; }
}

public class ProviderBookingShowViewModel
{
    public Booking Booking { get; set; } = null!;
    public Models.ServiceProvider Me { get; set; } = null!;
    public List<Booking> DayRows { get; set; } = new();
    public bool EndPassed { get; set; }
    public int WithYou { get; set; }
    public bool Returning { get; set; }
}