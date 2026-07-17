using FootballPrediction.ML.DataModels;
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

        // If ML model is loaded, use it for prediction
        if (_modelLoaded)
            return PredictWithModel(model);

        // Fallback: odds-based prediction
        double homeProb = 0.45, drawProb = 0.28, awayProb = 0.27;
        string result = "1";
        double confidence = 0.45;
        string fallbackComment = "Home win likely";

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

            fallbackComment = confidence switch
            {
                > 0.6 when result == "1" => "Strong home favorite",
                > 0.6 when result == "2" => "Strong away favorite",
                > 0.5 when result == "1" => "Home win likely",
                > 0.5 when result == "2" => "Away win likely",
                > 0.4 => "Balanced match",
                _ => "Highly uncertain"
            };
        }

        return View("Result", new PredictionResultViewModel
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
            Comment = fallbackComment,
            ModelUsed = "Odds-based (no model loaded)"
        });
    }

    private IActionResult PredictWithModel(PredictionInputViewModel model)
    {
        var matchData = BuildMatchDataFromInput(model);
        var predictions = _predictor.Predict(new[] { matchData });
        var pred = predictions[0];

        var comment = pred.Probability1 switch
        {
            > 0.6f when pred.PredictedResult == "1" => "Strong home favorite",
            > 0.6f when pred.PredictedResult == "2" => "Strong away favorite",
            > 0.5f when pred.PredictedResult == "1" => "Home win likely",
            > 0.5f when pred.PredictedResult == "2" => "Away win likely",
            > 0.4f => "Balanced match",
            _ => "Highly uncertain"
        };

        return View("Result", new PredictionResultViewModel
        {
            Date = model.Date ?? DateTime.Today,
            League = model.League,
            HomeTeam = model.HomeTeam,
            AwayTeam = model.AwayTeam,
            PredictedResult = pred.PredictedResult,
            Probability1 = Math.Round(pred.Probability1, 3),
            ProbabilityX = Math.Round(pred.ProbabilityX, 3),
            Probability2 = Math.Round(pred.Probability2, 3),
            Confidence = Math.Round(Math.Max(pred.Probability1, Math.Max(pred.ProbabilityX, pred.Probability2)), 3),
            Comment = comment,
            ModelUsed = "ML Model (SdcaMaximumEntropy)"
        });
    }

    private static MatchData BuildMatchDataFromInput(PredictionInputViewModel model)
    {
        float bet365Home = 0.33f, bet365Draw = 0.34f, bet365Away = 0.33f;
        float pinHome = 0.33f, pinDraw = 0.34f, pinAway = 0.33f;

        if (model.Bet365Home.HasValue && model.Bet365Draw.HasValue && model.Bet365Away.HasValue &&
            model.Bet365Home > 1 && model.Bet365Draw > 1 && model.Bet365Away > 1)
        {
            var (hp, dp, ap) = OddsNormalizer.Normalize(
                model.Bet365Home.Value, model.Bet365Draw.Value, model.Bet365Away.Value);
            bet365Home = (float)hp;
            bet365Draw = (float)dp;
            bet365Away = (float)ap;
        }

        if (model.PinnacleHome.HasValue && model.PinnacleDraw.HasValue && model.PinnacleAway.HasValue &&
            model.PinnacleHome > 1 && model.PinnacleDraw > 1 && model.PinnacleAway > 1)
        {
            var (hp, dp, ap) = OddsNormalizer.Normalize(
                model.PinnacleHome.Value, model.PinnacleDraw.Value, model.PinnacleAway.Value);
            pinHome = (float)hp;
            pinDraw = (float)dp;
            pinAway = (float)ap;
        }

        return new MatchData
        {
            League = model.League,
            HomeTeam = model.HomeTeam,
            AwayTeam = model.AwayTeam,
            HomeCoach = model.HomeCoach ?? "Unknown",
            AwayCoach = model.AwayCoach ?? "Unknown",
            Season = string.Empty,
            Bet365HomeProb = bet365Home,
            Bet365DrawProb = bet365Draw,
            Bet365AwayProb = bet365Away,
            PinnacleHomeProb = pinHome,
            PinnacleDrawProb = pinDraw,
            PinnacleAwayProb = pinAway,
            HomeForm5 = 0,
            AwayForm5 = 0,
            HomeGoalsForAvg = 0,
            AwayGoalsForAvg = 0,
            HomeGoalsAgainstAvg = 0,
            AwayGoalsAgainstAvg = 0,
            FormDiff = 0,
            HomeCoachTenure = 0,
            AwayCoachTenure = 0
        };
    }
}
