using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers;

[Route("chat")]
public class ChatController : BaseController
{
    private readonly AppDbContext _db;

    public ChatController(AppDbContext db) => _db = db;

    private string Role()
    {
        if (AuthId("provider") is not null)
        {
            return "provider";
        }

        if (AuthId("customer") is null)
        {
            throw new HttpResponseException(403);
        }

        return "customer";
    }

    private int MeId(string role) =>
        (role == "provider" ? AuthId("provider") : AuthId("customer")) ?? 0;

    private IQueryable<Message> Scope(string role) =>
        role == "provider"
            ? _db.Messages.Where(m => m.ProviderID == MeId(role))
            : _db.Messages.Where(m => m.CustomerID == MeId(role));

    private static string Initials(string name)
    {
        var parts = System.Text.RegularExpressions.Regex.Split(name.Trim(), @"\s+");
        var first = parts.Length > 0 && parts[0].Length > 0 ? parts[0][..1] : string.Empty;
        var second = parts.Length > 1 && parts[1].Length > 0 ? parts[1][..1] : string.Empty;
        return (first + second).ToUpperInvariant();
    }

    private static string? Photo(string? url) => Img.Url(url);

    [HttpGet("threads", Name = "chat.threads")]
    public IActionResult Threads()
    {
        var role = Role();

        var ids = role == "provider"
            ? Scope(role).Select(m => m.CustomerID).Distinct().ToList()
            : Scope(role).Select(m => m.ProviderID).Distinct().ToList();

        var threads = (role == "provider"
                ? _db.Customers.Where(c => ids.Contains(c.CustomerID)).ToList()
                    .Select(c => new { Id = c.CustomerID, Name = c.FullName, c.FullName_ = c.FullName, c.ProfilePhotoURL })
                    .Cast<dynamic>().ToList()
                : _db.ServiceProviders.Where(p => ids.Contains(p.ProviderID)).ToList()
                    .Select(p => new { Id = p.ProviderID, Name = p.BusinessName, FullName_ = p.FullName, p.ProfilePhotoURL })
                    .Cast<dynamic>().ToList())
            .Select(p =>
            {
                int id = p.Id;

                var last = role == "provider"
                    ? Scope(role).Where(m => m.CustomerID == id).OrderByDescending(m => m.SentAt).FirstOrDefault()
                    : Scope(role).Where(m => m.ProviderID == id).OrderByDescending(m => m.SentAt).FirstOrDefault();

                var incoming = role == "provider" ? "Customer" : "Provider";

                var unread = role == "provider"
                    ? Scope(role).Count(m => m.CustomerID == id && m.SenderType == incoming && !m.IsRead)
                    : Scope(role).Count(m => m.ProviderID == id && m.SenderType == incoming && !m.IsRead);

                return new
                {
                    id,
                    name = (string)p.Name,
                    initials = Initials((string)p.FullName_),
                    photo = Photo((string?)p.ProfilePhotoURL),
                    last = last?.MessageText,
                    sentAt = last is null ? 0 : new DateTimeOffset(last.SentAt).ToUnixTimeSeconds(),
                    unread,
                };
            })
            .OrderByDescending(t => t.sentAt)
            .ToList();

        return Json(new { threads });
    }

    [HttpGet("thread/{id:int}", Name = "chat.thread")]
    public IActionResult Thread(int id)
    {
        var role = Role();

        object? person = role == "provider" ? _db.Customers.Find(id) : _db.ServiceProviders.Find(id);

        if (person is null)
        {
            throw new HttpResponseException(404);
        }

        var incoming = role == "provider" ? "Customer" : "Provider";

        var cleared = role == "provider"
            ? Scope(role).Count(m => m.CustomerID == id && m.SenderType == incoming && !m.IsRead)
            : Scope(role).Count(m => m.ProviderID == id && m.SenderType == incoming && !m.IsRead);

        if (role == "provider")
        {
            Scope(role).Where(m => m.CustomerID == id && m.SenderType == incoming)
                .ExecuteUpdate(s => s.SetProperty(m => m.IsRead, true));
        }
        else
        {
            Scope(role).Where(m => m.ProviderID == id && m.SenderType == incoming)
                .ExecuteUpdate(s => s.SetProperty(m => m.IsRead, true));
        }

        var mine = role == "provider" ? "Provider" : "Customer";

        var rows = role == "provider"
            ? Scope(role).Where(m => m.CustomerID == id).OrderBy(m => m.SentAt).ToList()
            : Scope(role).Where(m => m.ProviderID == id).OrderBy(m => m.SentAt).ToList();

        var messages = rows.Select(m => new
        {
            id = m.MessageID,
            text = m.MessageText,
            time = m.SentAt.ToString("d MMM · HH:mm"),
            mine = m.SenderType == mine,
        });

        var name = person is Models.Customer c ? c.FullName : ((Models.ServiceProvider)person).BusinessName;

        var sub = person is Models.Customer c2
            ? c2.City
            : ((Models.ServiceProvider)person).FullName + " · " + ((Models.ServiceProvider)person).City;

        return Json(new { name, sub, messages, unreadCleared = cleared });
    }

    [HttpPost("thread/{id:int}", Name = "chat.send")]
    public IActionResult Send(int id, [FromForm] string? MessageText)
    {
        var role = Role();

        if (string.IsNullOrWhiteSpace(MessageText) || MessageText.Length > 1000)
        {
            return BadRequest(new { errors = new { MessageText = new[] { "Write a message first." } } });
        }

        object? person = role == "provider" ? _db.Customers.Find(id) : _db.ServiceProviders.Find(id);

        if (person is null)
        {
            throw new HttpResponseException(404);
        }

        var m = new Message
        {
            CustomerID = role == "provider" ? id : MeId(role),
            ProviderID = role == "provider" ? MeId(role) : id,
            SenderType = role == "provider" ? "Provider" : "Customer",
            MessageText = MessageText,
            IsRead = false,
            SentAt = DateTime.Now,
        };

        _db.Messages.Add(m);
        _db.SaveChanges();

        return Json(new { id = m.MessageID, text = m.MessageText, time = m.SentAt.ToString("d MMM · HH:mm") });
    }

    [HttpDelete("message/{messageId:int}", Name = "chat.message.destroy")]
    public IActionResult DestroyMessage(int messageId)
    {
        var role = Role();

        var m = Scope(role).FirstOrDefault(x => x.MessageID == messageId)
                ?? throw new HttpResponseException(404);

        if (m.SenderType != (role == "provider" ? "Provider" : "Customer"))
        {
            throw new HttpResponseException(403);
        }

        _db.Messages.Remove(m);
        _db.SaveChanges();

        return Json(new { ok = true });
    }

    [HttpDelete("thread/{id:int}", Name = "chat.thread.destroy")]
    public IActionResult DestroyThread(int id)
    {
        var role = Role();

        if (role == "provider")
        {
            Scope(role).Where(m => m.CustomerID == id).ExecuteDelete();
        }
        else
        {
            Scope(role).Where(m => m.ProviderID == id).ExecuteDelete();
        }

        return Json(new { ok = true });
    }
}