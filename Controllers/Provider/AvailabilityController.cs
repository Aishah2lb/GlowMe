using System.ComponentModel.DataAnnotations;
using GlowMe.Data;
using GlowMe.Models;
using GlowMe.Support.Middleware;
using GlowMe.Support.Rules;
using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers.Provider;

[Route("provider")]
public class ProviderAvailabilityController : BaseController
{
    private readonly AppDbContext _db;

    public ProviderAvailabilityController(AppDbContext db) => _db = db;

    [HttpPost("availability", Name = "provider.availability.store")]
    [GlowAuth("provider")]
    [ValidateAntiForgeryToken]
    public IActionResult Store(AvailabilityForm form)
    {
        var me = CurrentProvider(_db)!;

        if (!ModelState.IsValid)
        {
            return RedirectToAction("Index", "ProviderSchedule");
        }

        if (form.EndTime <= form.StartTime)
        {
            ModelState.AddModelError(nameof(form.EndTime), "The end time must be after the start time.");
            return RedirectToAction("Index", "ProviderSchedule");
        }

        var date = form.AvailableDate!.Value.Date;

        if (form.SlotStatus == "Blocked")
        {
            var inside = _db.Availabilities.Any(a =>
                a.ProviderID == me.ProviderID && a.SlotStatus == "Open" && a.AvailableDate == date
                && a.StartTime <= form.StartTime && a.EndTime >= form.EndTime);

            if (!inside)
            {
                ModelState.AddModelError(nameof(form.SlotStatus), "Block a time inside your working hours.");
                return RedirectToAction("Index", "ProviderSchedule");
            }
        }

        _db.Availabilities.Add(new Availability
        {
            ProviderID = me.ProviderID,
            AvailableDate = date,
            StartTime = form.StartTime,
            EndTime = form.EndTime,
            SlotStatus = form.SlotStatus,
        });
        _db.SaveChanges();

        Flash(form.SlotStatus == "Open" ? "Working hours added." : "Time blocked.");
        return RedirectToAction("Index", "ProviderSchedule");
    }
}

public class AvailabilityForm
{
    [Display(Name = "Date")]
    [Required]
    [DataType(DataType.Date)]
    public DateTime? AvailableDate { get; set; }

    [Display(Name = "Time")]
    [Required]
    public TimeSpan StartTime { get; set; }

    [Display(Name = "End time")]
    [Required]
    public TimeSpan EndTime { get; set; }

    [Display(Name = "Type")]
    [Required]
    [OneOf("Open", "Blocked")]
    public string SlotStatus { get; set; } = "Open";
}