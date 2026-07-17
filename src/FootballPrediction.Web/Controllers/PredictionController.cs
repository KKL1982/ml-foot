using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FootballPrediction.Web.Controllers;

public class PredictionController : Controller
{
    private readonly IPredictionService _predictionService;
    private readonly ModelSettings _settings;
    private readonly ILogger<PredictionController> _logger;

    public PredictionController(
        IPredictionService predictionService,
        IOptions<ModelSettings> options,
        ILogger<PredictionController> logger)
    {
        _predictionService = predictionService;
        _settings = options.Value;
        _logger = logger;
        _logger.LogInformation("PredictionController initialized — mode: {Mode}", _settings.DefaultMode);
    }

    [HttpGet]
    public IActionResult Index()
    {
        var model = new PredictionInputViewModel
        {
            ModelLoaded = true,
            ModelPath = Path.Combine(Directory.GetCurrentDirectory(), _settings.BinaryModelPath),
            Mode = _settings.DefaultMode
        };
        return View(model);
    }

    [HttpPost]
    public IActionResult Predict(PredictionInputViewModel model)
    {
        if (!ModelState.IsValid)
            return View("Index", model);

        var input = MapToDto(model);

        if (model.Mode == "Multiclass")
            return PredictMulticlass(input);

        return PredictBinary(input, model.BinaryThreshold);
    }

    private IActionResult PredictBinary(PredictionInputDto input, double threshold)
    {
        var pred = _predictionService.PredictBinary(input, threshold);

        _logger.LogInformation("Binary prediction: {Home} vs {Away} → {Bet} ({Conf:P0})",
            pred.HomeTeam, pred.AwayTeam, pred.Bet, pred.Confidence);

        return View("Result", new PredictionResultViewModel
        {
            Date = pred.Date,
            League = pred.League,
            HomeTeam = pred.HomeTeam,
            AwayTeam = pred.AwayTeam,
            PredictedResult = pred.Bet,
            Probability1 = pred.HomeWinProbability,
            ProbabilityX = 0,
            Probability2 = pred.AwayWinProbability,
            Confidence = pred.Confidence,
            Comment = pred.Comment,
            ModelUsed = "ML Model (Binary)",
            Mode = "Binary",
            HomeWinProbability = pred.HomeWinProbability,
            AwayWinProbability = pred.AwayWinProbability
        });
    }

    private IActionResult PredictMulticlass(PredictionInputDto input)
    {
        var pred = _predictionService.PredictMulticlass(input);

        _logger.LogInformation("Multiclass prediction: {Home} vs {Away} → {Result} ({Conf:P0})",
            pred.HomeTeam, pred.AwayTeam, pred.PredictedResult, pred.Confidence);

        return View("Result", new PredictionResultViewModel
        {
            Date = pred.Date,
            League = pred.League,
            HomeTeam = pred.HomeTeam,
            AwayTeam = pred.AwayTeam,
            PredictedResult = pred.PredictedResult,
            Probability1 = pred.Probability1,
            ProbabilityX = pred.ProbabilityX,
            Probability2 = pred.Probability2,
            Confidence = pred.Confidence,
            Comment = pred.Comment,
            ModelUsed = "ML Model (Multiclass)",
            Mode = "Multiclass"
        });
    }

    private static PredictionInputDto MapToDto(PredictionInputViewModel model) => new()
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
}
