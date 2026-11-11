using GlowMe.Data;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Provider;

[Route("provider")]
public class ProviderNotificationController : BaseController
{
    private readonly AppDbContext _db;

    public ProviderNotificationController(AppDbContext db) => _db = db;

    [HttpGet("notifications", Name = "provider.notifications")]
    [GlowAuth("provider")]
    public IActionResult Index([FromQuery] int page = 1)
    {
        var me = CurrentProvider(_db)!;

        var notifications = Paginated<Models.Notification>.From(
            _db.Notifications
                .Where(n => n.ProviderID == me.ProviderID)
                .OrderByDescending(n => n.CreatedAt),
            page, 20);

        _db.Notifications
            .Where(n => n.ProviderID == me.ProviderID && !n.IsRead)
            .ExecuteUpdate(s => s.SetProperty(n => n.IsRead, true));

        return View("~/Views/Provider/Notifications.cshtml", new ProviderNotificationsViewModel
        {
            Me = me,
            Notifications = notifications,
        });
    }

    [HttpPost("notifications/{notification:int}/delete", Name = "provider.notifications.destroy")]
    [HttpDelete("notifications/{notification:int}")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Destroy(int notification)
    {
        var row = _db.Notifications.Find(notification);

        if (row is null || row.ProviderID != AuthId("provider"))
        {
            throw new HttpResponseException(403);
        }

        _db.Notifications.Remove(row);
        _db.SaveChanges();

        Flash("Notification deleted.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("notifications/delete-all", Name = "provider.notifications.destroyAll")]
    [HttpDelete("notifications")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult DestroyAll()
    {
        var id = AuthId("provider");

        _db.Notifications.Where(n => n.ProviderID == id).ExecuteDelete();

        Flash("All notifications were deleted.");
        return RedirectToAction(nameof(Index));
    }
}

public class ProviderNotificationsViewModel
{
    public Models.ServiceProvider Me { get; set; } = null!;
    public Paginated<Models.Notification> Notifications { get; set; } = new();
}