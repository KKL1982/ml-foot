using FootballPrediction.ML.FeatureEngineering;
using FootballPrediction.ML.Prediction;
using FootballPrediction.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace FootballPrediction.Web.Controllers;

public class PredictionController : Controller
{
    private readonly MatchPredictor _predictor;
    private readonly string _modelPath;
    private bool _modelLoaded;

    public PredictionController()
    {
        _predictor = new MatchPredictor();
        // Try to load model from default location
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
        var model = new PredictionInputViewModel
        {
            ModelLoaded = _modelLoaded,
            ModelPath = _modelLoaded ? _modelPath : null
        };
        return View(model);
    }

    [HttpPost]
    public IActionResult Predict(PredictionInputViewModel model)
    {
        if (!ModelState.IsValid)
            return View("Index", model);

        double homeProb = 0.45, drawProb = 0.28, awayProb = 0.27;
        string result = "1";
        double confidence = 0.45;
        string comment = "Home win likely";

        if (model.Bet365Home.HasValue && model.Bet365Draw.HasValue && model.Bet365Away.HasValue &&
            model.Bet365Home > 1 && model.Bet365Draw > 1 && model.Bet365Away > 1)
        {
            var (hp, dp, ap) = OddsNormalizer.Normalize(
                model.Bet365Home.Value, model.Bet365Draw.Value, model.Bet365Away.Value);

            homeProb = hp;
            drawProb = dp;
            awayProb = ap;

            confidence = Math.Max(hp, Math.Max(dp, ap));
            if (hp >= dp && hp >= ap) result = "1";
            else if (dp >= hp && dp >= ap) result = "X";
            else result = "2";

            comment = confidence switch
            {
                > 0.6 when result == "1" => "Strong home favorite",
                > 0.6 when result == "2" => "Strong away favorite",
                > 0.5 when result == "1" => "Home win likely",
                > 0.5 when result == "2" => "Away win likely",
                > 0.4 => "Balanced match",
                _ => "Highly uncertain"
            };
        }

        var resultModel = new PredictionResultViewModel
        {
            Date = model.Date ?? DateTime.Today,
            League = model.League,
            HomeTeam = model.HomeTeam,
            AwayTeam = model.AwayTeam,
            PredictedResult = result,
            Probability1 = Math.Round(homeProb, 3),
            ProbabilityX = Math.Round(drawProb, 3),
            Probability2 = Math.Round(awayProb, 3),
            Confidence = Math.Round(confidence, 3),
            Comment = comment,
            ModelUsed = _modelLoaded ? "ML Model (SdcaMaximumEntropy)" : "Odds-based (no model loaded)"
        };

        return View("Result", resultModel);
    }
}
