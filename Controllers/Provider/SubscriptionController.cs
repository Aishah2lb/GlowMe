using System.ComponentModel.DataAnnotations;
using System.Globalization;
using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using GlowMe.Support.Rules;
using GlowMe.Support.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Provider;

[Route("provider")]
public class ProviderSubscriptionController : BaseController
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notify;

    public ProviderSubscriptionController(AppDbContext db, NotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    [HttpGet("subscription", Name = "provider.subscription")]
    [GlowAuth("provider")]
    public IActionResult Index()
    {
        var me = _db.ServiceProviders
            .Include(p => p.Subscriptions).ThenInclude(s => s.Plan)
            .First(p => p.ProviderID == AuthId("provider"));

        var current = me.ActiveSubscription();

        var plans = _db.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToList();

        var history = me.Subscriptions
            .OrderByDescending(s => s.StartDate)
            .ToList();

        var subIds = history.Select(s => s.SubscriptionID).ToList();

        var payments = _db.Payments
            .Where(p => p.SubscriptionID != null && subIds.Contains(p.SubscriptionID.Value))
            .OrderByDescending(p => p.PaidAt)
            .ToList();

        return View("~/Views/Provider/Subscription.cshtml", new ProviderSubscriptionViewModel
        {
            Me = me,
            Current = current,
            Plans = plans,
            History = history,
            Payments = payments,
        });
    }

    [HttpPost("subscription", Name = "provider.subscription.subscribe")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Subscribe(SubscribeForm form)
    {
        var me = CurrentProvider(_db)!;

        var plan = form.PlanID is null
            ? null
            : _db.SubscriptionPlans.FirstOrDefault(p => p.PlanID == form.PlanID && p.IsActive);

        if (plan is null)
        {
            ModelState.AddModelError(nameof(form.PlanID), "Choose a plan.");
        }

        if (!ModelState.IsValid)
        {
            return Index();
        }

        using var tx = _db.Database.BeginTransaction();

        _db.Subscriptions
            .Where(s => s.ProviderID == me.ProviderID && s.Status == "Active")
            .ExecuteUpdate(s => s.SetProperty(x => x.Status, "Cancelled"));

        var start = DateTime.Today;
        var end = DateTime.Today.AddDays(plan!.DurationDays);

        var sub = new Subscription
        {
            ProviderID = me.ProviderID,
            PlanID = plan.PlanID,
            StartDate = start,
            EndDate = end,
            Status = "Active",
            CreatedAt = DateTime.Now,
        };
        _db.Subscriptions.Add(sub);
        _db.SaveChanges();

        _db.Payments.Add(new Payment
        {
            SubscriptionID = sub.SubscriptionID,
            Amount = plan.Price,
            PaymentType = "Subscription",
            PaymentMethod = form.PaymentMethod,
            PaymentStatus = "Paid",
            TransactionReference = "GW-" + RandomString(8).ToUpperInvariant(),
            PaidAt = DateTime.Now,
        });
        _db.SaveChanges();

        _notify.ToProvider(me.ProviderID, "Subscription",
            $"Your {plan.PlanName} plan is active until "
            + end.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) + ".");

        tx.Commit();

        Flash("Your subscription is active.");
        return RedirectToAction(nameof(Index));
    }

    private static string RandomString(int length)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Range(0, length)
            .Select(_ => alphabet[Random.Shared.Next(alphabet.Length)])
            .ToArray());
    }
}

public class SubscribeForm
{
    [Required(ErrorMessage = "Choose a plan.")]
    public int? PlanID { get; set; }

    [Display(Name = "Payment method")]
    [Required(ErrorMessage = "Choose a payment method.")]
    [OneOf("Mada", "Credit card", "Apple Pay", "STC Pay",
        ErrorMessage = "Choose a payment method.")]
    public string PaymentMethod { get; set; } = string.Empty;
}

public class ProviderSubscriptionViewModel
{
    public Models.ServiceProvider Me { get; set; } = null!;
    public Subscription? Current { get; set; }
    public List<SubscriptionPlan> Plans { get; set; } = new();
    public List<Subscription> History { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}