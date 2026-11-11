using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Customer;

public class CustomerMessageController : BaseController
{
    private readonly AppDbContext _db;

    public CustomerMessageController(AppDbContext db) => _db = db;

    [HttpGet("/messages", Name = "messages.index")]
    [GlowAuth("customer")]
    public IActionResult Index([FromQuery(Name = "with")] int? with)
    {
        var me = CurrentCustomer(_db)!;

        var providerIds = _db.Messages
            .Where(m => m.CustomerID == me.CustomerID)
            .Select(m => m.ProviderID)
            .Distinct()
            .ToList();

        var threads = _db.ServiceProviders
            .Where(p => providerIds.Contains(p.ProviderID))
            .ToList()
            .Select(p => new MessageThread
            {
                Provider = p,
                LastMessage = _db.Messages
                    .Where(m => m.CustomerID == me.CustomerID && m.ProviderID == p.ProviderID)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefault(),
                Unread = _db.Messages.Count(m =>
                    m.CustomerID == me.CustomerID && m.ProviderID == p.ProviderID
                    && m.SenderType == "Provider" && !m.IsRead),
            })
            .OrderByDescending(t => t.LastMessage?.SentAt)
            .ToList();

        var active = with is not null
            ? _db.ServiceProviders.Find(with)
            : threads.FirstOrDefault()?.Provider;

        var messages = new List<Message>();

        if (active is not null)
        {
            _db.Messages
                .Where(m => m.CustomerID == me.CustomerID && m.ProviderID == active.ProviderID
                            && m.SenderType == "Provider")
                .ExecuteUpdate(s => s.SetProperty(m => m.IsRead, true));

            messages = _db.Messages
                .Where(m => m.CustomerID == me.CustomerID && m.ProviderID == active.ProviderID)
                .OrderBy(m => m.SentAt)
                .ToList();
        }

        return View("~/Views/Customer/Messages.cshtml", new CustomerMessagesViewModel
        {
            Me = me,
            Threads = threads,
            Active = active,
            Messages = messages,
        });
    }

    [HttpPost("/messages/{provider:int}", Name = "messages.send")]
    [GlowAuth("customer")]
    [ValidateAntiForgeryToken]
    public IActionResult Send(int provider, [FromForm] string? MessageText)
    {
        var me = CurrentCustomer(_db)!;

        if (string.IsNullOrWhiteSpace(MessageText) || MessageText.Length > 1000)
        {
            ModelState.AddModelError("MessageText", "Write a message first.");
            return RedirectToAction(nameof(Index), new { with = provider });
        }

        _db.Messages.Add(new Message
        {
            CustomerID = me.CustomerID,
            ProviderID = provider,
            SenderType = "Customer",
            MessageText = MessageText,
            IsRead = false,
            SentAt = DateTime.Now,
        });
        _db.SaveChanges();

        return RedirectToAction(nameof(Index), new { with = provider });
    }
}

public class MessageThread
{
    public Models.ServiceProvider Provider { get; set; } = null!;
    public Message? LastMessage { get; set; }
    public int Unread { get; set; }
}

public class CustomerMessagesViewModel
{
    public Models.Customer Me { get; set; } = null!;
    public List<MessageThread> Threads { get; set; } = new();
    public Models.ServiceProvider? Active { get; set; }
    public List<Message> Messages { get; set; } = new();
}