using System.ComponentModel.DataAnnotations;
using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Admin;

[Route("admin")]
public class AdminPlanController : BaseController
{
    private readonly AppDbContext _db;

    public AdminPlanController(AppDbContext db) => _db = db;

    [HttpGet("plans", Name = "admin.plans")]
    [GlowAuth("admin")]
    public IActionResult Index()
    {
        var plans = _db.SubscriptionPlans
            .OrderBy(p => p.Price)
            .Select(p => new PlanRow
            {
                Plan = p,
                SubscriptionsCount = p.Subscriptions.Count,
            })
            .ToList();

        return View("~/Views/Admin/Plans.cshtml", plans);
    }

    [HttpPost("plans", Name = "admin.plans.store")]
    [GlowAuth("admin")]
    [ValidateAntiForgeryToken]
    public IActionResult Store(PlanForm form)
    {
        ValidatePlan(form, null);

        if (!ModelState.IsValid)
        {
            return Index();
        }

        _db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            PlanName = form.PlanName,
            Description = form.Description,
            Price = form.Price!.Value,
            DurationDays = form.DurationDays!.Value,
            IsActive = true,
        });
        _db.SaveChanges();

        Flash("Plan added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("plans/{plan:int}", Name = "admin.plans.update")]
    [GlowAuth("admin")]
    [ValidateAntiForgeryToken]
    public IActionResult Update(int plan, PlanForm form)
    {
        var row = _db.SubscriptionPlans.Find(plan) ?? throw new HttpResponseException(404);

        ValidatePlan(form, row.PlanID);

        if (!ModelState.IsValid)
        {
            return Index();
        }

        row.PlanName = form.PlanName;
        row.Description = form.Description;
        row.Price = form.Price!.Value;
        row.DurationDays = form.DurationDays!.Value;
        _db.SaveChanges();

        Flash("Plan updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("plans/{plan:int}/toggle", Name = "admin.plans.toggle")]
    [GlowAuth("admin")]
    [ValidateAntiForgeryToken]
    public IActionResult Toggle(int plan)
    {
        var row = _db.SubscriptionPlans.Find(plan) ?? throw new HttpResponseException(404);

        row.IsActive = !row.IsActive;
        _db.SaveChanges();

        Flash(row.IsActive ? "Plan activated." : "Plan deactivated.");
        return RedirectToAction(nameof(Index));
    }

    private void ValidatePlan(PlanForm form, int? ignore)
    {
        var clash = _db.SubscriptionPlans.Any(p =>
            p.PlanName == form.PlanName && (ignore == null || p.PlanID != ignore));

        if (clash)
        {
            ModelState.AddModelError(nameof(form.PlanName), "A plan with this name already exists.");
        }
    }
}

public class PlanRow
{
    public SubscriptionPlan Plan { get; set; } = null!;
    public int SubscriptionsCount { get; set; }
}

public class PlanForm
{
    [Display(Name = "Plan name")]
    [Required]
    [MaxLength(60)]
    public string PlanName { get; set; } = string.Empty;

    [Display(Name = "Description")]
    [MaxLength(300)]
    public string? Description { get; set; }

    [Display(Name = "Price")]
    [Required]
    [Range(0, double.MaxValue)]
    public decimal? Price { get; set; }

    [Display(Name = "Duration in days")]
    [Required]
    [Range(1, 366)]
    public int? DurationDays { get; set; }
}