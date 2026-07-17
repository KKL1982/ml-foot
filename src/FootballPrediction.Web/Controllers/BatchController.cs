using FootballPrediction.Application.Services;
using FootballPrediction.ML.FeatureEngineering;
using FootballPrediction.ML.Prediction;
using FootballPrediction.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace FootballPrediction.Web.Controllers;

public class BatchController : Controller
{
    private readonly MatchPredictor _predictor;
    private readonly bool _modelLoaded;
    private readonly string _modelPath;

    public BatchController()
    {
        _predictor = new MatchPredictor();
        _modelPath = Path.Combine(Directory.GetCurrentDirectory(), "models", "model.zip");
        try
        {
            if (System.IO.File.Exists(_modelPath))
            {
                _predictor.LoadModel(_modelPath);
                _modelLoaded = true;
            }
        }
        catch
        {
            _modelLoaded = false;
        }
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

            var parser = new CsvParserService();
            var matches = await parser.ParseMatchesAsync(tempPath);

            var features = FeatureEngineer.BuildFeatures(matches);

            List<PredictionResultViewModel> predictions;

            if (_modelLoaded && features.Count > 0)
            {
                // Use ML model
                var mlPredictions = _predictor.Predict(features);
                predictions = features.Select((f, i) =>
                {
                    var pred = mlPredictions[i];
                    double maxProb = Math.Max(Math.Max(pred.Probability1, pred.ProbabilityX), pred.Probability2);

                    string comment = maxProb switch
                    {
                        > 0.6 when pred.PredictedResult == "1" => "Strong home favorite",
                        > 0.6 when pred.PredictedResult == "2" => "Strong away favorite",
                        > 0.5 when pred.PredictedResult == "1" => "Home win likely",
                        > 0.5 when pred.PredictedResult == "2" => "Away win likely",
                        > 0.4 => "Balanced match",
                        _ => "Highly uncertain"
                    };

                    return new PredictionResultViewModel
                    {
                        Date = matches[i].Date,
                        League = matches[i].League,
                        HomeTeam = matches[i].HomeTeam,
                        AwayTeam = matches[i].AwayTeam,
                        PredictedResult = pred.PredictedResult,
                        Probability1 = Math.Round(pred.Probability1, 3),
                        ProbabilityX = Math.Round(pred.ProbabilityX, 3),
                        Probability2 = Math.Round(pred.Probability2, 3),
                        Confidence = Math.Round(maxProb, 3),
                        Comment = comment,
                        ModelUsed = "ML Model (SdcaMaximumEntropy)"
                    };
                }).ToList();
            }
            else
            {
                // Fallback: odds-based (Bet365 when available, otherwise Pinnacle)
                predictions = features.Select((f, i) =>
                {
                    // Prefer Bet365, fall back to Pinnacle
                    float homeP = f.Bet365HomeProb, drawP = f.Bet365DrawProb, awayP = f.Bet365AwayProb;
                    if (Math.Abs(homeP + drawP + awayP - 1.0) > 0.01 &&
                        Math.Abs(f.PinnacleHomeProb + f.PinnacleDrawProb + f.PinnacleAwayProb - 1.0) < 0.01)
                    {
                        homeP = f.PinnacleHomeProb;
                        drawP = f.PinnacleDrawProb;
                        awayP = f.PinnacleAwayProb;
                    }

                    double maxProb = Math.Max(Math.Max(homeP, drawP), awayP);
                    string result = homeP >= drawP && homeP >= awayP ? "1"
                        : drawP >= homeP && drawP >= awayP ? "X"
                        : "2";

                    string comment = maxProb switch
                    {
                        > 0.6 when result == "1" => "Strong home favorite",
                        > 0.6 when result == "2" => "Strong away favorite",
                        > 0.5 when result == "1" => "Home win likely",
                        > 0.5 when result == "2" => "Away win likely",
                        > 0.4 => "Balanced match",
                        _ => "Highly uncertain"
                    };

                    return new PredictionResultViewModel
                    {
                        Date = matches[i].Date,
                        League = matches[i].League,
                        HomeTeam = matches[i].HomeTeam,
                        AwayTeam = matches[i].AwayTeam,
                        PredictedResult = result,
                        Probability1 = Math.Round(homeP, 3),
                        ProbabilityX = Math.Round(drawP, 3),
                        Probability2 = Math.Round(awayP, 3),
                        Confidence = Math.Round(maxProb, 3),
                        Comment = comment,
                        ModelUsed = "Odds-based (no model loaded)"
                    };
                }).ToList();
            }

            var model = new BatchResultViewModel
            {
                TotalMatches = matches.Count,
                SuccessfulPredictions = predictions.Count,
                Predictions = predictions
            };

            return View("Result", model);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }
}
