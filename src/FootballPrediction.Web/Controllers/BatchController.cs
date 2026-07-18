using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.ML.FeatureEngineering;
using FootballPrediction.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;

namespace FootballPrediction.Web.Controllers;

[Authorize]
public class BatchController : Controller
{
    private readonly IPredictionService _predictionService;
    private readonly ICsvParser _csvParser;
    private readonly ModelSettings _settings;
    private readonly ILogger<BatchController> _logger;

    public BatchController(
        IPredictionService predictionService,
        ICsvParser csvParser,
        IOptions<ModelSettings> options,
        ILogger<BatchController> logger)
    {
        _predictionService = predictionService;
        _csvParser = csvParser;
        _settings = options.Value;
        _logger = logger;
        _logger.LogInformation("BatchController initialized — mode: {Mode}", _settings.DefaultMode);
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("file", "Please select a CSV file.");
            return View("Index");
        }

        var tempPath = Path.GetTempFileName();
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var matches = await _csvParser.ParseMatchesAsync(tempPath);
            var features = FeatureEngineer.BuildFeatures(matches);

            var inputs = features.Select(f => new PredictionInputDto
            {
                Date = DateTime.Today,
                League = f.League,
                HomeTeam = f.HomeTeam,
                AwayTeam = f.AwayTeam,
                Bet365Home = f.Bet365HomeProb > 0 ? 1.0 / f.Bet365HomeProb : null,
                Bet365Draw = f.Bet365DrawProb > 0 ? 1.0 / f.Bet365DrawProb : null,
                Bet365Away = f.Bet365AwayProb > 0 ? 1.0 / f.Bet365AwayProb : null,
                PinnacleHome = f.PinnacleHomeProb > 0 ? 1.0 / f.PinnacleHomeProb : null,
                PinnacleDraw = f.PinnacleDrawProb > 0 ? 1.0 / f.PinnacleDrawProb : null,
                PinnacleAway = f.PinnacleAwayProb > 0 ? 1.0 / f.PinnacleAwayProb : null
            }).ToList();

            var binaryPreds = _predictionService.PredictBatchBinary(inputs);

            var csv = new StringBuilder();
            csv.AppendLine("Date,League,HomeTeam,AwayTeam,Bet,HomeWinProbability,Confidence,Comment");
            var viewModels = new List<PredictionResultViewModel>();
            for (int i = 0; i < binaryPreds.Count; i++)
            {
                var bp = binaryPreds[i];
                csv.AppendLine($"{bp.Date:yyyy-MM-dd},{Escape(bp.League)},{Escape(bp.HomeTeam)},{Escape(bp.AwayTeam)},{bp.Bet},{bp.HomeWinProbability:F3},{bp.Confidence:F3},{Escape(bp.Comment)}");
                viewModels.Add(new PredictionResultViewModel
                {
                    Date = bp.Date,
                    League = bp.League,
                    HomeTeam = bp.HomeTeam,
                    AwayTeam = bp.AwayTeam,
                    PredictedResult = bp.Bet,
                    Probability1 = bp.HomeWinProbability,
                    Probability2 = bp.AwayWinProbability,
                    Confidence = bp.Confidence,
                    Comment = bp.Comment,
                    ModelUsed = "ML Model (Binary)",
                    Mode = "Binary",
                    HomeWinProbability = bp.HomeWinProbability,
                    AwayWinProbability = bp.AwayWinProbability
                });
            }

            TempData["BatchPredictionsCsv"] = csv.ToString();

            _logger.LogInformation("Batch processed: {Total} matches, {Predictions} predictions",
                matches.Count, binaryPreds.Count);

            return View("Result", new BatchResultViewModel
            {
                TotalMatches = matches.Count,
                SuccessfulPredictions = binaryPreds.Count,
                Predictions = viewModels,
                DownloadFileName = $"predictions_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            });
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    [HttpGet]
    public IActionResult Download()
    {
        var csv = TempData["BatchPredictionsCsv"] as string;
        if (string.IsNullOrEmpty(csv))
            return RedirectToAction("Index");

        var fileName = $"predictions_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    private static string Escape(string value) => value.Contains(',') ? $"\"{value}\"" : value;
}
