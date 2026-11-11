using Microsoft.AspNetCore.Mvc;

namespace GlowMe.Controllers;

public class StaticController : BaseController
{
    [HttpGet("/terms", Name = "terms")]
    public IActionResult Terms()
    {
        return View("~/Views/Static/Terms.cshtml");
    }

    [HttpGet("/privacy", Name = "privacy")]
    public IActionResult Privacy()
    {
        return View("~/Views/Static/Privacy.cshtml");
    }
}