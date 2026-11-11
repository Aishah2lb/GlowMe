using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Provider;

[Route("provider")]
public class ProviderMessageController : BaseController
{
    private readonly AppDbContext _db;

    public ProviderMessageController(AppDbContext db) => _db = db;

    [HttpGet("messages", Name = "provider.messages")]
    [GlowAuth("provider")]
    public IActionResult Index([FromQuery(Name = "with")] int? with)
    {
        var me = CurrentProvider(_db)!;

        var customerIds = _db.Messages
            .Where(m => m.ProviderID == me.ProviderID)
            .Select(m => m.CustomerID)
            .Distinct()
            .ToList();

        var threads = _db.Customers
            .Where(c => customerIds.Contains(c.CustomerID))
            .ToList()
            .Select(c => new CustomerThread
            {
                Customer = c,
                LastMessage = _db.Messages
                    .Where(m => m.ProviderID == me.ProviderID && m.CustomerID == c.CustomerID)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefault(),
                Unread = _db.Messages.Count(m =>
                    m.ProviderID == me.ProviderID && m.CustomerID == c.CustomerID
                    && m.SenderType == "Customer" && !m.IsRead),
            })
            .OrderByDescending(t => t.LastMessage?.SentAt)
            .ToList();

        var active = with is not null
            ? _db.Customers.Find(with)
            : threads.FirstOrDefault()?.Customer;

        var messages = new List<Message>();

        if (active is not null)
        {
            _db.Messages
                .Where(m => m.ProviderID == me.ProviderID && m.CustomerID == active.CustomerID
                            && m.SenderType == "Customer")
                .ExecuteUpdate(s => s.SetProperty(m => m.IsRead, true));

            messages = _db.Messages
                .Where(m => m.ProviderID == me.ProviderID && m.CustomerID == active.CustomerID)
                .OrderBy(m => m.SentAt)
                .ToList();
        }

        return View("~/Views/Provider/Messages.cshtml", new ProviderMessagesViewModel
        {
            Me = me,
            Threads = threads,
            Active = active,
            Messages = messages,
        });
    }

    [HttpPost("messages/{customer:int}", Name = "provider.messages.send")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Send(int customer, [FromForm] string? MessageText)
    {
        var me = CurrentProvider(_db)!;

        if (string.IsNullOrWhiteSpace(MessageText) || MessageText.Length > 1000)
        {
            ModelState.AddModelError("MessageText", "Write a message first.");
            return RedirectToAction(nameof(Index), new { with = customer });
        }

        _db.Messages.Add(new Message
        {
            CustomerID = customer,
            ProviderID = me.ProviderID,
            SenderType = "Provider",
            MessageText = MessageText,
            IsRead = false,
            SentAt = DateTime.Now,
        });
        _db.SaveChanges();

        return RedirectToAction(nameof(Index), new { with = customer });
    }
}

public class CustomerThread
{
    public Models.Customer Customer { get; set; } = null!;
    public Message? LastMessage { get; set; }
    public int Unread { get; set; }
}

public class ProviderMessagesViewModel
{
    public Models.ServiceProvider Me { get; set; } = null!;
    public List<CustomerThread> Threads { get; set; } = new();
    public Models.Customer? Active { get; set; }
    public List<Message> Messages { get; set; } = new();
}