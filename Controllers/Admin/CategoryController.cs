using System.ComponentModel.DataAnnotations;
using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support;
using GlowMe.Support.Exceptions;
using GlowMe.Support.Middleware;
using GlowMe.Support.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowMe.Controllers.Admin;

[Route("admin")]
public class AdminCategoryController : BaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public AdminCategoryController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet("categories", Name = "admin.categories")]
    [GlowAuth("admin")]
    public IActionResult Index()
    {
        var categories = _db.Categories
            .OrderBy(c => c.CategoryID)
            .Select(c => new CategoryRow
            {
                Category = c,
                ServicesCount = c.Services.Count,
            })
            .ToList();

        return View("~/Views/Admin/Categories.cshtml", new AdminCategoriesViewModel
        {
            Categories = categories,
            SupportsImage = Category.SupportsImage(_db),
            Form = new CategoryForm(),
        });
    }

    [HttpPost("categories", Name = "admin.categories.store")]
    [GlowAuth("admin")]
    [ValidateAntiForgeryToken]
    public IActionResult Store(CategoryForm form)
    {
        ValidateCat(form, null);

        if (!ModelState.IsValid)
        {
            return Index();
        }

        var category = new Category
        {
            CategoryName = form.CategoryName,
            Description = form.Description,
            AdminID = AuthId("admin")!.Value,
        };

        WithImage(form, category, null);

        _db.Categories.Add(category);
        _db.SaveChanges();

        Flash("Category added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("categories/{category:int}", Name = "admin.categories.update")]
    [GlowAuth("admin")]
    [ValidateAntiForgeryToken]
    public IActionResult Update(int category, CategoryForm form)
    {
        var row = _db.Categories.Find(category) ?? throw new HttpResponseException(404);

        ValidateCat(form, row.CategoryID);

        if (!ModelState.IsValid)
        {
            return Index();
        }

        row.CategoryName = form.CategoryName;
        row.Description = form.Description;

        WithImage(form, row, row);

        _db.SaveChanges();

        Flash("Category updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("categories/{category:int}/delete", Name = "admin.categories.destroy")]
    [HttpDelete("categories/{category:int}")]
    [GlowAuth("admin")]
    [ValidateAntiForgeryToken]
    public IActionResult Destroy(int category)
    {
        var row = _db.Categories.Find(category) ?? throw new HttpResponseException(404);

        var used = _db.Services.Count(s => s.CategoryID == row.CategoryID);

        if (used > 0)
        {
            TempData["categoryError"] =
                $"{row.CategoryName} still has {used} {(used == 1 ? "service" : "services")}. "
                + "Move them to another category or delete them first.";

            return RedirectToAction(nameof(Index));
        }

        var image = row.ImageURL;

        try
        {
            _db.Categories.Remove(row);
            _db.SaveChanges();
        }
        catch (DbUpdateException)
        {
            TempData["categoryError"] =
                row.CategoryName + " is still linked to other records and cannot be deleted.";

            return RedirectToAction(nameof(Index));
        }

        Img.Delete(image, _env);

        Flash(row.CategoryName + " was deleted.");
        return RedirectToAction(nameof(Index));
    }

    private void ValidateCat(CategoryForm form, int? ignore)
    {
        var clash = _db.Categories.Any(c =>
            c.CategoryName == form.CategoryName && (ignore == null || c.CategoryID != ignore));

        if (clash)
        {
            ModelState.AddModelError(nameof(form.CategoryName), "A category with this name already exists.");
        }
    }

    private void WithImage(CategoryForm form, Category target, Category? existing)
    {
        if (!Category.SupportsImage(_db))
        {
            return;
        }

        if (form.image_file is not null)
        {
            target.ImageURL = Img.Store(form.image_file, "categories", _env);

            if (existing is not null)
            {
                Img.Delete(existing.ImageURL, _env);
            }
        }
        else if (!string.IsNullOrEmpty(form.image_url))
        {
            target.ImageURL = form.image_url;
        }
        else if (form.image_clear)
        {
            target.ImageURL = null;
        }
    }
}

public class CategoryRow
{
    public Category Category { get; set; } = null!;
    public int ServicesCount { get; set; }
}

public class CategoryForm
{
    [Display(Name = "Category name")]
    [Required]
    [MaxLength(50)]
    public string CategoryName { get; set; } = string.Empty;

    [Display(Name = "Description")]
    [MaxLength(300)]
    public string? Description { get; set; }

    [Url(ErrorMessage = "Enter a full image address starting with http.")]
    [MaxLength(500)]
    public string? image_url { get; set; }

    [AllowedFile("jpg", "jpeg", "png", "webp",
        ImageOnly = true,
        MaxKilobytes = 5120,
        ImageMessage = "Upload a JPG, PNG or WebP picture.",
        MaxMessage = "Upload a picture up to 5 MB.")]
    public IFormFile? image_file { get; set; }

    public bool image_clear { get; set; }
}

public class AdminCategoriesViewModel
{
    public List<CategoryRow> Categories { get; set; } = new();
    public bool SupportsImage { get; set; }
    public CategoryForm Form { get; set; } = new();
}