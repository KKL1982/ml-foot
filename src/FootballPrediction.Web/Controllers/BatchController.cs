using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Application.Services;
using FootballPrediction.ML.FeatureEngineering;
using FootballPrediction.Web.Models;
using FootballPrediction.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;

namespace FootballPrediction.Web.Controllers;

[Authorize]
public class BatchController : Controller
{
    private readonly IPredictionService _predictionService;
    private readonly BatchPredictionService _batchPredictionService;
    private readonly ICsvParser _csvParser;
    private readonly ModelSettings _settings;
    private readonly ILogger<BatchController> _logger;

    public BatchController(
        IPredictionService predictionService,
        BatchPredictionService batchPredictionService,
        ICsvParser csvParser,
        IOptions<ModelSettings> options,
        ILogger<BatchController> logger)
    {
        _predictionService = predictionService;
        _batchPredictionService = batchPredictionService;
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

            // Prédiction batch enrichie (multiclass 1/X/2 + coachs à jour + bet).
            var items = _batchPredictionService.Predict(matches);

            var csv = new StringBuilder();
            csv.AppendLine(BatchResultFormatter.CsvHeader);
            var viewModels = new List<PredictionResultViewModel>();
            foreach (var it in items)
            {
                csv.AppendLine(BatchResultFormatter.ToCsv(new[] { it }).Trim());
                viewModels.Add(new PredictionResultViewModel
                {
                    Date = it.Date,
                    League = it.League,
                    HomeTeam = it.HomeTeam,
                    AwayTeam = it.AwayTeam,
                    HomeCoach = it.HomeCoach,
                    AwayCoach = it.AwayCoach,
                    PredictedResult = it.PredictedResult,
                    Probability1 = it.Probability1,
                    ProbabilityX = it.ProbabilityX,
                    Probability2 = it.Probability2,
                    Confidence = it.Confidence,
                    Comment = it.Comment,
                    HomeWinProbability = it.Probability1,
                    AwayWinProbability = it.Probability2,
                    ModelUsed = "ML Model (Batch)",
                    Mode = "Multiclass"
                });
            }

            TempData["BatchPredictionsCsv"] = csv.ToString();

            _logger.LogInformation("Batch processed: {Total} matches, {Predictions} predictions",
                matches.Count, items.Count);

            return View("Result", new BatchResultViewModel
            {
                TotalMatches = matches.Count,
                SuccessfulPredictions = items.Count,
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
