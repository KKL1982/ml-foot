using FootballPrediction.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FootballPrediction.Web.Controllers;

public class HomeController : Controller
{
    private readonly ModelSettings _settings;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IOptions<ModelSettings> options, ILogger<HomeController> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["ModelMode"] = _settings.DefaultMode;
        _logger.LogInformation("Dashboard loaded — mode: {Mode}", _settings.DefaultMode);
        return View();
    }
}
