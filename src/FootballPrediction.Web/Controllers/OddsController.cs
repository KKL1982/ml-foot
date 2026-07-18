using FootballPrediction.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FootballPrediction.Web.Controllers;

[Authorize]
[Route("[controller]")]
public class OddsController : Controller
{
    private readonly IOddsFetcherService _oddsFetcher;
    private readonly ILogger<OddsController> _logger;

    public OddsController(IOddsFetcherService oddsFetcher, ILogger<OddsController> logger)
    {
        _oddsFetcher = oddsFetcher;
        _logger = logger;
    }

    /// <summary>
    /// AJAX endpoint: GET /Odds/fetch?league=...&homeTeam=...&awayTeam=...
    /// </summary>
    [HttpGet("fetch")]
    public async Task<IActionResult> Fetch(string league, string homeTeam, string awayTeam)
    {
        if (string.IsNullOrWhiteSpace(league) || string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
        {
            return BadRequest(new { error = "league, homeTeam, and awayTeam are required." });
        }

        var result = await _oddsFetcher.FetchOddsAsync(league, homeTeam, awayTeam);
        return Ok(result);
    }
}
