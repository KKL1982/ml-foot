using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Application.Services;
using FootballPrediction.Web.Models;
using FootballPrediction.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FootballPrediction.Web.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class ApiController : ControllerBase
{
    private readonly IPredictionService _predictionService;
    private readonly BatchPredictionService _batchPredictionService;
    private readonly ModelSettings _settings;
    private readonly ILogger<ApiController> _logger;
    private readonly MatchFetchService? _matchFetchService;

    public ApiController(
        IPredictionService predictionService,
        BatchPredictionService batchPredictionService,
        IOptions<ModelSettings> options,
        ILogger<ApiController> logger,
        MatchFetchService? matchFetchService = null)
    {
        _matchFetchService = matchFetchService;
        _predictionService = predictionService;
        _batchPredictionService = batchPredictionService;
        _settings = options.Value;
        _logger = logger;
    }

    [HttpPost("predict")]
    public ActionResult<BinaryPredictionOutputDto> PredictBinary([FromBody] PredictionInputViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var input = new PredictionInputDto
        {
            Date = model.Date ?? DateTime.Today,
            League = model.League,
            HomeTeam = model.HomeTeam,
            AwayTeam = model.AwayTeam,
            HomeCoach = model.HomeCoach,
            AwayCoach = model.AwayCoach,
            Bet365Home = model.Bet365Home,
            Bet365Draw = model.Bet365Draw,
            Bet365Away = model.Bet365Away,
            PinnacleHome = model.PinnacleHome,
            PinnacleDraw = model.PinnacleDraw,
            PinnacleAway = model.PinnacleAway
        };

        var result = _predictionService.PredictBinary(input, model.BinaryThreshold);
        _logger.LogInformation("API binary: {Home} vs {Away} → {Bet}", result.HomeTeam, result.AwayTeam, result.Bet);
        return Ok(result);
    }

    [HttpPost("predict/multiclass")]
    public ActionResult<PredictionOutputDto> PredictMulticlass([FromBody] PredictionInputViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var input = new PredictionInputDto
        {
            Date = model.Date ?? DateTime.Today,
            League = model.League,
            HomeTeam = model.HomeTeam,
            AwayTeam = model.AwayTeam,
            HomeCoach = model.HomeCoach,
            AwayCoach = model.AwayCoach,
            Bet365Home = model.Bet365Home,
            Bet365Draw = model.Bet365Draw,
            Bet365Away = model.Bet365Away,
            PinnacleHome = model.PinnacleHome,
            PinnacleDraw = model.PinnacleDraw,
            PinnacleAway = model.PinnacleAway
        };

        var result = _predictionService.PredictMulticlass(input);
        _logger.LogInformation("API multiclass: {Home} vs {Away} → {Result}", result.HomeTeam, result.AwayTeam, result.PredictedResult);
        return Ok(result);
    }

    [HttpGet("health")]
    public ActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            mode = _settings.DefaultMode,
            modelPath = _settings.BinaryModelPath,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Prédit un batch de matchs NON encore joués (reçus en JSON) et retourne les
    /// résultats enrichis (coachs à jour, probabilités 1/X/2, confiance, bet).
    /// </summary>
    [HttpPost("batch/predict")]
    public ActionResult<IReadOnlyList<BatchPredictionItem>> PredictBatch([FromBody] List<PredictionInputViewModel> model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var matches = model.Select(m => new Domain.Entities.Match
        {
            Date = m.Date ?? DateTime.Today,
            League = m.League,
            HomeTeam = m.HomeTeam,
            AwayTeam = m.AwayTeam,
            HomeCoach = m.HomeCoach,
            AwayCoach = m.AwayCoach,
        }).ToList();

        var items = _batchPredictionService.Predict(matches, threshold: 0.5);
        _logger.LogInformation("API batch: {Count} matches predicted", items.Count);
        return Ok(items);
    }

    /// <summary>
    /// Récupère les matchs d'une période (championnats séparés par des virgules, ex.
    /// league=PL,LL) depuis la source externe et retourne leurs prédictions.
    /// </summary>
    [HttpGet("batch/predict")]
    public async Task<ActionResult<IReadOnlyList<BatchPredictionItem>>> PredictBatchByPeriod(
        [FromQuery] string league, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        if (_matchFetchService == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Récupération des matchs indisponible.");
        if (string.IsNullOrWhiteSpace(league) || to < from || (to - from).TotalDays > 31)
            return BadRequest("Paramètres invalides : league, from <= to, période de 31 jours maximum.");

        var fetched = await _matchFetchService.FetchAsync(league.Split(',', StringSplitOptions.RemoveEmptyEntries), from, to, ct);
        if (fetched.Matches.Count == 0 && fetched.Errors.Count > 0)
            return StatusCode(StatusCodes.Status502BadGateway, string.Join(" — ", fetched.Errors));

        return Ok(_batchPredictionService.Predict(fetched.Matches, threshold: 0.5));
    }
}
