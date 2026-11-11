using GlowMe.Data;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers;

public abstract class BaseController : Controller
{
    protected void Flash(string message) => TempData["flash"] = message;

    protected void FlashError(string message) => TempData["error"] = message;

    protected int? AuthId(string guard)
    {
        if (HttpContext.User.Identity?.IsAuthenticated != true || !User.IsInRole(guard))
        {
            return null;
        }

        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    protected Models.Customer? CurrentCustomer(AppDbContext db)
    {
        var id = AuthId("customer");
        return id is null ? null : db.Customers.Find(id);
    }

    protected Models.ServiceProvider? CurrentProvider(AppDbContext db)
    {
        var id = AuthId("provider");
        return id is null ? null : db.ServiceProviders.Find(id);
    }

    protected Models.Admin? CurrentAdmin(AppDbContext db)
    {
        var id = AuthId("admin");
        return id is null ? null : db.Admins.Find(id);
    }
}