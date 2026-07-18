using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Web.Models;
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
    private readonly ModelSettings _settings;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        IPredictionService predictionService,
        IOptions<ModelSettings> options,
        ILogger<ApiController> logger)
    {
        _predictionService = predictionService;
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
}
