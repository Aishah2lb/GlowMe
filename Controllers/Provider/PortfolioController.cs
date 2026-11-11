using System.ComponentModel.DataAnnotations;
using GlowMe.Data;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using GlowMe.Support.Rules;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Provider;

[Route("provider")]
public class ProviderPortfolioController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProviderPortfolioController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet("portfolio", Name = "provider.portfolio")]
    [GlowAuth("provider")]
    public IActionResult Index()
    {
        var me = CurrentProvider(_db)!;

        var photos = _db.Portfolios
            .Where(p => p.ProviderID == me.ProviderID)
            .OrderByDescending(p => p.UploadedAt)
            .ToList();

        return View("~/Views/Provider/Portfolio.cshtml", new ProviderPortfolioViewModel
        {
            Me = me,
            Photos = photos,
        });
    }

    [HttpPost("portfolio", Name = "provider.portfolio.store")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Store(PortfolioForm form)
    {
        var me = CurrentProvider(_db)!;

        if (!ModelState.IsValid)
        {
            return Index();
        }

        var path = Img.Store(form.Photo!, $"portfolio/{me.ProviderID}", _env);

        _db.Portfolios.Add(new Models.Portfolio
        {
            ProviderID = me.ProviderID,
            PhotoURL = path,
            Caption = form.Caption,
            UploadedAt = DateTime.Now,
        });
        _db.SaveChanges();

        Flash("Photo added to your portfolio.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("portfolio/{photo:int}/delete", Name = "provider.portfolio.destroy")]
    [HttpDelete("portfolio/{photo:int}")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Destroy(int photo)
    {
        var row = _db.Portfolios.Find(photo);

        if (row is null || row.ProviderID != AuthId("provider"))
        {
            throw new HttpResponseException(403);
        }

        Img.Delete(row.PhotoURL, _env);
        _db.Portfolios.Remove(row);
        _db.SaveChanges();

        Flash("Photo removed.");
        return RedirectToAction(nameof(Index));
    }
}

public class PortfolioForm
{
    [Display(Name = "Photo")]
    [Required(ErrorMessage = "Upload a photo up to 5 MB.")]
    [AllowedFile("jpg", "jpeg", "png", "webp",
        ImageOnly = true,
        MaxKilobytes = 5120,
        MaxMessage = "Upload a photo up to 5 MB.")]
    public IFormFile? Photo { get; set; }

    [Display(Name = "Caption")]
    [MaxLength(120)]
    public string? Caption { get; set; }
}

public class ProviderPortfolioViewModel
{
    public Models.ServiceProvider Me { get; set; } = null!;
    public List<Models.Portfolio> Photos { get; set; } = new();
}