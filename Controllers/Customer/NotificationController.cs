using GlowMe.Data;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Customer;

public class CustomerNotificationController : BaseController
{
    private readonly AppDbContext _db;

    public CustomerNotificationController(AppDbContext db) => _db = db;

    [HttpGet("/notifications", Name = "notifications.index")]
    [GlowAuth("customer")]
    public IActionResult Index([FromQuery] int page = 1)
    {
        var me = CurrentCustomer(_db)!;

        var notifications = Paginated<Models.Notification>.From(
            _db.Notifications
                .Where(n => n.CustomerID == me.CustomerID)
                .OrderByDescending(n => n.CreatedAt),
            page, 20);

        _db.Notifications
            .Where(n => n.CustomerID == me.CustomerID && !n.IsRead)
            .ExecuteUpdate(s => s.SetProperty(n => n.IsRead, true));

        return View("~/Views/Customer/Notifications.cshtml", new NotificationsViewModel
        {
            Me = me,
            Notifications = notifications,
        });
    }

    [HttpPost("/notifications/{notification:int}/delete", Name = "notifications.destroy")]
    [HttpDelete("/notifications/{notification:int}")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult Destroy(int notification)
    {
        var row = _db.Notifications.Find(notification);

        if (row is null || row.CustomerID != AuthId("customer"))
        {
            throw new HttpResponseException(403);
        }

        _db.Notifications.Remove(row);
        _db.SaveChanges();

        Flash("Notification deleted.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/notifications/delete-all", Name = "notifications.destroyAll")]
    [HttpDelete("/notifications")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult DestroyAll()
    {
        var id = AuthId("customer");

        _db.Notifications.Where(n => n.CustomerID == id).ExecuteDelete();

        Flash("All notifications were deleted.");
        return RedirectToAction(nameof(Index));
    }
}

public class NotificationsViewModel
{
    public Models.Customer Me { get; set; } = null!;
    public Paginated<Models.Notification> Notifications { get; set; } = new();
}