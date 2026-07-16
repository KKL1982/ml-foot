using Microsoft.AspNetCore.Mvc;

namespace FootballPrediction.Web.Controllers;

public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
