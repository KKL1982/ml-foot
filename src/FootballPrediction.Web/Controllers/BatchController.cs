using FootballPrediction.Application.Services;
using FootballPrediction.ML.FeatureEngineering;
using FootballPrediction.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace FootballPrediction.Web.Controllers;

public class BatchController : Controller
{
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

            var predictions = features.Select((f, i) =>
            {
                double maxProb = Math.Max(Math.Max(f.Bet365HomeProb, f.Bet365DrawProb), f.Bet365AwayProb);
                string result = f.Bet365HomeProb >= f.Bet365DrawProb && f.Bet365HomeProb >= f.Bet365AwayProb ? "1"
                    : f.Bet365DrawProb >= f.Bet365HomeProb && f.Bet365DrawProb >= f.Bet365AwayProb ? "X"
                    : "2";

                string comment = maxProb switch
                {
                    > 0.6f when result == "1" => "Strong home favorite",
                    > 0.6f when result == "2" => "Strong away favorite",
                    > 0.5f when result == "1" => "Home win likely",
                    > 0.5f when result == "2" => "Away win likely",
                    > 0.4f => "Balanced match",
                    _ => "Highly uncertain"
                };

                return new PredictionResultViewModel
                {
                    Date = matches[i].Date,
                    League = matches[i].League,
                    HomeTeam = matches[i].HomeTeam,
                    AwayTeam = matches[i].AwayTeam,
                    PredictedResult = result,
                    Probability1 = Math.Round(f.Bet365HomeProb, 3),
                    ProbabilityX = Math.Round(f.Bet365DrawProb, 3),
                    Probability2 = Math.Round(f.Bet365AwayProb, 3),
                    Confidence = Math.Round(maxProb, 3),
                    Comment = comment
                };
            }).ToList();

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
