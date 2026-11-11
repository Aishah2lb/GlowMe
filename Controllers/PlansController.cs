using GlowMe.Data;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers;

public class PlansController : BaseController
{
    private readonly AppDbContext _db;

    public PlansController(AppDbContext db) => _db = db;

    [HttpGet("/plans", Name = "plans.public")]
    public IActionResult Index()
    {
        var plans = _db.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToList();

        return View("~/Views/Public/Plans.cshtml", plans);
    }
}