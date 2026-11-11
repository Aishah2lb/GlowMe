using System.Security.Claims;
using System.Text.Json;
using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using GlowMe.Support.Services;
using GlowMe.ViewModels.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Auth;

public class RegisterController : BaseController
{
    private readonly AppDbContext _db;
    private readonly AccountLinks _links;
    private readonly IWebHostEnvironment _env;

    public RegisterController(AppDbContext db, AccountLinks links, IWebHostEnvironment env)
    {
        _db = db;
        _links = links;
        _env = env;
    }

    [HttpGet("/register", Name = "register")]
    [GlowGuest]
    public IActionResult Type()
    {
        return View("~/Views/Auth/RegisterType.cshtml");
    }

    [HttpPost("/register", Name = "register.choose")]
    [GlowGuest]
    [ValidateAntiForgeryToken]
    public IActionResult ChooseType([FromForm] string? account_type)
    {
        if (account_type is not ("customer" or "provider"))
        {
            ModelState.AddModelError("account_type", "Choose how you will use GlowMe.");
            return View("~/Views/Auth/RegisterType.cshtml");
        }

        HttpContext.Session.SetString("register.account_type", account_type);

        return account_type == "customer"
            ? RedirectToAction(nameof(CustomerForm))
            : RedirectToAction(nameof(ProviderForm));
    }

    [HttpGet("/register/customer", Name = "register.customer")]
    [GlowGuest]
    public IActionResult CustomerForm()
    {
        return View("~/Views/Auth/RegisterCustomer.cshtml", new CustomerRegisterViewModel());
    }

    [HttpPost("/register/customer", Name = "register.customer.store")]
    [GlowGuest]
    [ValidateAntiForgeryToken]
    public IActionResult StoreCustomer(CustomerRegisterViewModel model)
    {
        model.PrepareForValidation();

        if (!ModelState.IsValid)
        {
            return View("~/Views/Auth/RegisterCustomer.cshtml", model);
        }

        var customer = new Models.Customer
        {
            FullName = model.FullName,
            Email = model.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            PhoneNumber = model.PhoneNumber,
            City = model.City,
            AccountStatus = "Pending",
            CreatedAt = DateTime.Now,
        };

        _db.Customers.Add(customer);
        _db.SaveChanges();

        StartVerification("customer", customer.CustomerID, customer.Email, customer.PasswordHash);
        return RedirectToAction(nameof(VerifyNotice));
    }

    [HttpGet("/register/provider", Name = "register.provider")]
    [GlowGuest]
    public IActionResult ProviderForm()
    {
        ViewData["categories"] = _db.Categories.OrderBy(c => c.CategoryID).ToList();

        var draft = ReadProviderDraft();
        return View("~/Views/Auth/RegisterProvider.cshtml", draft ?? new ProviderRegisterViewModel());
    }

    [HttpGet("/register/provider/plan", Name = "register.provider.plan")]
    [GlowGuest]
    public IActionResult ProviderPlanForm()
    {
        var draft = ReadProviderDraft();

        if (draft is null)
        {
            return RedirectToAction(nameof(ProviderForm));
        }

        ViewData["draft"] = draft;

        var plans = _db.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToList();

        return View("~/Views/Auth/RegisterProviderPlan.cshtml", plans);
    }

    [HttpPost("/register/provider", Name = "register.provider.store")]
    [GlowGuest]
    [ValidateAntiForgeryToken]
    public IActionResult StoreProvider(ProviderRegisterViewModel model)
    {
        model.PrepareForValidation();

        if (!ModelState.IsValid)
        {
            ViewData["categories"] = _db.Categories.OrderBy(c => c.CategoryID).ToList();
            return View("~/Views/Auth/RegisterProvider.cshtml", model);
        }

        if (model.License is not null)
        {
            var previous = ReadProviderDraft()?.LicenseURL;
            if (!string.IsNullOrEmpty(previous))
            {
                Img.Delete(previous, _env);
            }

            model.LicenseURL = Img.Store(model.License, "licenses", _env);
        }

        model.License = null;

        WriteProviderDraft(model);
        return RedirectToAction(nameof(ProviderPlanForm));
    }

    [HttpPost("/register/provider/plan", Name = "register.provider.plan.store")]
    [GlowGuest]
    [ValidateAntiForgeryToken]
    public IActionResult StoreProviderPlan([FromForm] int? PlanID)
    {
        var draft = ReadProviderDraft();

        if (draft is null)
        {
            return RedirectToAction(nameof(ProviderForm));
        }

        var plan = PlanID is null
            ? null
            : _db.SubscriptionPlans.FirstOrDefault(p => p.PlanID == PlanID && p.IsActive);

        if (plan is null)
        {
            ModelState.AddModelError("PlanID", "Choose the plan you want to start with.");
            ViewData["draft"] = draft;
            return View("~/Views/Auth/RegisterProviderPlan.cshtml",
                _db.SubscriptionPlans.Where(p => p.IsActive).OrderBy(p => p.Price).ToList());
        }

        var provider = new Models.ServiceProvider
        {
            FullName = draft.FullName,
            BusinessName = draft.BusinessName,
            Email = draft.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(draft.Password),
            PhoneNumber = draft.PhoneNumber,
            City = draft.City,
            OffersHomeService = draft.OffersHomeService ?? false,
            AccountStatus = "Pending",
            CreatedAt = DateTime.Now,
        };

        if (Models.ServiceProvider.SupportsLicense(_db) && !string.IsNullOrEmpty(draft.LicenseURL))
        {
            provider.LicenseURL = draft.LicenseURL;
            provider.LicenseStatus = "Pending";
        }

        _db.ServiceProviders.Add(provider);
        _db.SaveChanges();

        _db.Subscriptions.Add(new Subscription
        {
            ProviderID = provider.ProviderID,
            PlanID = plan.PlanID,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(plan.DurationDays),
            Status = "Active",
            CreatedAt = DateTime.Now,
        });
        _db.SaveChanges();

        HttpContext.Session.SetString("provider.signup_categories",
            JsonSerializer.Serialize(draft.Categories));
        HttpContext.Session.Remove("register.provider");

        StartVerification("provider", provider.ProviderID, provider.Email, provider.PasswordHash);
        return RedirectToAction(nameof(VerifyNotice));
    }

    private void StartVerification(string type, int id, string email, string hash)
    {
        var account = new PendingAccount
        {
            Type = type,
            Id = id,
            Email = email,
            Link = _links.Verify(type, id),
            SentAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
        };

        HttpContext.Session.SetString("pending_account", JsonSerializer.Serialize(account));
    }

    [HttpGet("/register/verify", Name = "register.verify")]
    public IActionResult VerifyNotice()
    {
        var account = ReadPendingAccount();

        if (account is null)
        {
            return RedirectToAction(nameof(Type));
        }

        return View("~/Views/Auth/Verify.cshtml", account);
    }

    [HttpPost("/register/verify/resend", Name = "register.resend")]
    [ValidateAntiForgeryToken]
    public IActionResult Resend()
    {
        var account = ReadPendingAccount();

        if (account is null)
        {
            return RedirectToAction(nameof(Type));
        }

        account.Link = _links.Verify(account.Type, account.Id);
        account.SentAt = DateTimeOffset.Now.ToUnixTimeSeconds();
        HttpContext.Session.SetString("pending_account", JsonSerializer.Serialize(account));

        TempData["resent"] = true;
        return RedirectToAction(nameof(VerifyNotice));
    }

    [HttpGet("/verify/{type}/{id:int}", Name = "verify")]
    public IActionResult Verify(string type, int id, [FromQuery] string? token)
    {
        if (token is null || _links.Unprotect(token) is null)
        {
            throw new HttpResponseException(403, "This verification link has expired. Please request a new one.");
        }

        object? model = type == "customer"
            ? _db.Customers.Find(id)
            : _db.ServiceProviders.Find(id);

        if (model is null)
        {
            throw new HttpResponseException(404);
        }

        switch (model)
        {
            case Models.Customer c when c.AccountStatus == "Pending":
                c.AccountStatus = "Active";
                break;
            case Models.ServiceProvider p when p.AccountStatus == "Pending":
                p.AccountStatus = "Active";
                break;
        }

        _db.SaveChanges();

        HttpContext.Session.Remove("pending_account");

        var guard = type == "customer" ? "customer" : "provider";
        SignIn(guard, id, model is Models.Customer c2 ? c2.FullName : ((Models.ServiceProvider)model).FullName);

        Flash("Your email is verified — welcome to GlowMe.");

        return type == "customer"
            ? RedirectToAction("Index", "Home")
            : RedirectToAction("Dashboard", "ProviderDashboard");
    }

    [HttpGet("/register/verify/again", Name = "register.verify.again")]
    public IActionResult Again([FromQuery] string? email)
    {
        var address = (email ?? string.Empty).Trim().ToLowerInvariant();

        var customer = _db.Customers
            .FirstOrDefault(c => c.Email.ToLower() == address && c.AccountStatus == "Pending");

        var provider = customer is not null
            ? null
            : _db.ServiceProviders
                .FirstOrDefault(p => p.Email.ToLower() == address && p.AccountStatus == "Pending");

        if (customer is null && provider is null)
        {
            return RedirectToAction("Show", "Auth");
        }

        if (customer is not null)
        {
            StartVerification("customer", customer.CustomerID, customer.Email, customer.PasswordHash);
        }
        else
        {
            StartVerification("provider", provider!.ProviderID, provider.Email, provider.PasswordHash);
        }

        return RedirectToAction(nameof(VerifyNotice));
    }

    [HttpPost("/register/verify/check", Name = "register.check")]
    [ValidateAntiForgeryToken]
    public IActionResult Check()
    {
        var account = ReadPendingAccount();

        if (account is null)
        {
            return RedirectToAction("Show", "Auth");
        }

        object? model = account.Type == "customer"
            ? _db.Customers.Find(account.Id)
            : _db.ServiceProviders.Find(account.Id);

        var status = model switch
        {
            Models.Customer c => c.AccountStatus,
            Models.ServiceProvider p => p.AccountStatus,
            _ => null,
        };

        if (model is not null && status == "Active")
        {
            HttpContext.Session.Remove("pending_account");

            var name = model is Models.Customer c2 ? c2.FullName : ((Models.ServiceProvider)model).FullName;
            SignIn(account.Type, account.Id, name);

            return account.Type == "customer"
                ? RedirectToAction("Index", "Home")
                : RedirectToAction("Dashboard", "ProviderDashboard");
        }

        TempData["notVerified"] = true;
        return RedirectToAction(nameof(VerifyNotice));
    }

    private ProviderRegisterViewModel? ReadProviderDraft()
    {
        var json = HttpContext.Session.GetString("register.provider");
        return json is null ? null : JsonSerializer.Deserialize<ProviderRegisterViewModel>(json);
    }

    private void WriteProviderDraft(ProviderRegisterViewModel model) =>
        HttpContext.Session.SetString("register.provider", JsonSerializer.Serialize(model));

    private PendingAccount? ReadPendingAccount()
    {
        var json = HttpContext.Session.GetString("pending_account");
        return json is null ? null : JsonSerializer.Deserialize<PendingAccount>(json);
    }

    private void SignIn(string guard, int id, string name)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, guard),
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal).GetAwaiter().GetResult();
    }
}

public class PendingAccount
{
    public string Type { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public long SentAt { get; set; }
}