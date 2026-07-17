using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.ML.DataModels;
using FootballPrediction.ML.FeatureEngineering;
using FootballPrediction.ML.Prediction;
using Microsoft.Extensions.Logging;

namespace FootballPrediction.Web.Services;

public class PredictionService : IPredictionService
{
    private readonly MatchPredictor _predictor;
    private readonly ILogger<PredictionService> _logger;

    public PredictionService(MatchPredictor predictor, ILogger<PredictionService> logger)
    {
        _predictor = predictor;
        _logger = logger;
    }

    public BinaryPredictionOutputDto PredictBinary(PredictionInputDto input, double threshold = 0.5)
    {
        var matchData = BuildMatchData(input);
        var predictions = _predictor.Predict(new[] { matchData });
        var pred = predictions[0];

        // Determine home probability from available columns
        double homeProb = pred.HomeWinProbability > 0
            ? pred.HomeWinProbability
            : pred.Probability1 > 0
                ? pred.Probability1
                : CalculateHomeProbFromOdds(input);

        double awayProb = 1 - homeProb;
        double confidence = Math.Max(homeProb, awayProb);
        string bet = confidence >= threshold
            ? (homeProb >= 0.5 ? "HOME" : "AWAY")
            : "SKIP";

        string comment = bet switch
        {
            "HOME" => $"Strong home signal ({homeProb:P0})",
            "AWAY" => $"Strong away signal ({awayProb:P0})",
            _ => "Below confidence threshold"
        };

        _logger.LogInformation("Binary prediction: {Home} vs {Away} → {Bet} (Home:{HomeProb:P0}, Conf:{Conf:P0})",
            input.HomeTeam, input.AwayTeam, bet, homeProb, confidence);

        return new BinaryPredictionOutputDto
        {
            Date = input.Date,
            League = input.League,
            HomeTeam = input.HomeTeam,
            AwayTeam = input.AwayTeam,
            HomeWinProbability = Math.Round(homeProb, 3),
            AwayWinProbability = Math.Round(awayProb, 3),
            Confidence = Math.Round(confidence, 3),
            Bet = bet,
            Comment = comment
        };
    }

    public PredictionOutputDto PredictMulticlass(PredictionInputDto input)
    {
        var matchData = BuildMatchData(input);
        var predictions = _predictor.Predict(new[] { matchData });
        var pred = predictions[0];

        // Fallback: if multiclass columns are empty (binary model loaded), synthesize from available data
        double p1 = pred.Probability1 > 0 ? pred.Probability1 :
            pred.HomeWinProbability > 0 ? pred.HomeWinProbability : CalculateHomeProbFromOdds(input);
        double p2 = pred.Probability2 > 0 ? pred.Probability2 :
            pred.HomeWinProbability > 0 ? 1 - pred.HomeWinProbability : 1 - p1;
        double pX = pred.ProbabilityX > 0 ? pred.ProbabilityX : 0;
        // Normalize
        double sum = p1 + pX + p2;
        if (sum > 0) { p1 /= sum; pX /= sum; p2 /= sum; }
        else { p1 = 0.45; pX = 0.28; p2 = 0.27; }

        string result = !string.IsNullOrEmpty(pred.PredictedResult) ? pred.PredictedResult
            : p1 >= pX && p1 >= p2 ? "1" : pX >= p1 && pX >= p2 ? "X" : "2";

        double conf = Math.Max(p1, Math.Max(pX, p2));
        string comment = p1 switch
        {
            > 0.6f when pred.PredictedResult == "1" => "Strong home favorite",
            > 0.6f when pred.PredictedResult == "2" => "Strong away favorite",
            > 0.5f when pred.PredictedResult == "1" => "Home win likely",
            > 0.5f when pred.PredictedResult == "2" => "Away win likely",
            > 0.4f => "Balanced match",
            _ => "Highly uncertain"
        };

        return new PredictionOutputDto
        {
            Date = input.Date,
            League = input.League,
            HomeTeam = input.HomeTeam,
            AwayTeam = input.AwayTeam,
            PredictedResult = result,
            Probability1 = Math.Round(p1, 3),
            ProbabilityX = Math.Round(pX, 3),
            Probability2 = Math.Round(p2, 3),
            Confidence = Math.Round(conf, 3),
            Comment = comment
        };
    }

    private static double CalculateHomeProbFromOdds(PredictionInputDto input)
    {
        if (input.Bet365Home.HasValue && input.Bet365Away.HasValue && input.Bet365Home > 1 && input.Bet365Away > 1)
        {
            double h = 1.0 / input.Bet365Home.Value;
            double a = 1.0 / input.Bet365Away.Value;
            return h / (h + a);
        }
        if (input.PinnacleHome.HasValue && input.PinnacleAway.HasValue && input.PinnacleHome > 1 && input.PinnacleAway > 1)
        {
            double h = 1.0 / input.PinnacleHome.Value;
            double a = 1.0 / input.PinnacleAway.Value;
            return h / (h + a);
        }
        return 0.5;
    }

    public IReadOnlyList<BinaryPredictionOutputDto> PredictBatchBinary(IReadOnlyList<PredictionInputDto> inputs, double threshold = 0.5)
    {
        return inputs.Select(i => PredictBinary(i, threshold)).ToList();
    }

    public IReadOnlyList<PredictionOutputDto> PredictBatchMulticlass(IReadOnlyList<PredictionInputDto> inputs)
    {
        return inputs.Select(PredictMulticlass).ToList();
    }

    private static MatchData BuildMatchData(PredictionInputDto input)
    {
        float bet365Home = 0.33f, bet365Draw = 0.34f, bet365Away = 0.33f;
        float pinHome = 0.33f, pinDraw = 0.34f, pinAway = 0.33f;

        if (input.Bet365Home.HasValue && input.Bet365Draw.HasValue && input.Bet365Away.HasValue &&
            input.Bet365Home > 1 && input.Bet365Draw > 1 && input.Bet365Away > 1)
        {
            var (hp, dp, ap) = OddsNormalizer.Normalize(
                input.Bet365Home.Value, input.Bet365Draw.Value, input.Bet365Away.Value);
            bet365Home = (float)hp; bet365Draw = (float)dp; bet365Away = (float)ap;
        }

        if (input.PinnacleHome.HasValue && input.PinnacleDraw.HasValue && input.PinnacleAway.HasValue &&
            input.PinnacleHome > 1 && input.PinnacleDraw > 1 && input.PinnacleAway > 1)
        {
            var (hp, dp, ap) = OddsNormalizer.Normalize(
                input.PinnacleHome.Value, input.PinnacleDraw.Value, input.PinnacleAway.Value);
            pinHome = (float)hp; pinDraw = (float)dp; pinAway = (float)ap;
        }

        return new MatchData
        {
            League = input.League,
            HomeTeam = input.HomeTeam,
            AwayTeam = input.AwayTeam,
            HomeCoach = input.HomeCoach ?? "Unknown",
            AwayCoach = input.AwayCoach ?? "Unknown",
            Season = input.Season,
            Bet365HomeProb = bet365Home,
            Bet365DrawProb = bet365Draw,
            Bet365AwayProb = bet365Away,
            PinnacleHomeProb = pinHome,
            PinnacleDrawProb = pinDraw,
            PinnacleAwayProb = pinAway,
            HomeForm5 = 0, AwayForm5 = 0,
            HomeGoalsForAvg = 0, AwayGoalsForAvg = 0,
            HomeGoalsAgainstAvg = 0, AwayGoalsAgainstAvg = 0,
            FormDiff = 0, HomeCoachTenure = 0, AwayCoachTenure = 0
        };
    }
}
