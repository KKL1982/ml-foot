using FootballPrediction.Domain.Entities;
using FootballPrediction.Domain.Enums;
using FootballPrediction.ML.DataModels;
using FootballPrediction.ML.FeatureEngineering;

namespace FootballPrediction.ML.FeatureEngineering;

public static class FeatureEngineer
{
    public static List<MatchData> BuildFeatures(IReadOnlyList<Match> matches)
    {
        var sorted = matches.OrderBy(m => m.Date).ToList();
        var result = new List<MatchData>(sorted.Count);

        foreach (var match in sorted)
        {
            if (match.Result == null) continue;
            if (match.HomeGoals == null || match.AwayGoals == null) continue;

            var homeForm = FormCalculator.CalculateForm(match.HomeTeam, match.Date, sorted);
            var awayForm = FormCalculator.CalculateForm(match.AwayTeam, match.Date, sorted);

            var homeProb = 0.33;
            var drawProb = 0.34;
            var awayProb = 0.33;

            if (match.BookmakerOdds.TryGetValue("Bet365", out var bet365))
            {
                var (hp, dp, ap) = OddsNormalizer.Normalize(
                    (double)bet365.HomeWin, (double)bet365.Draw, (double)bet365.AwayWin);
                homeProb = hp;
                drawProb = dp;
                awayProb = ap;
            }

            result.Add(new MatchData
            {
                League = HashString(match.League),
                HomeTeam = HashString(match.HomeTeam),
                AwayTeam = HashString(match.AwayTeam),
                Bet365HomeProb = (float)homeProb,
                Bet365DrawProb = (float)drawProb,
                Bet365AwayProb = (float)awayProb,
                HomeForm5 = (float)homeForm.PointsPerGame,
                AwayForm5 = (float)awayForm.PointsPerGame,
                HomeGoalsForAvg = (float)homeForm.GoalsForAvg,
                AwayGoalsForAvg = (float)awayForm.GoalsForAvg,
                HomeGoalsAgainstAvg = (float)homeForm.GoalsAgainstAvg,
                AwayGoalsAgainstAvg = (float)awayForm.GoalsAgainstAvg,
                FormDiff = (float)FormCalculator.FormDiff(homeForm, awayForm),
                Label = match.Result.Value.ToDisplayString()
            });
        }

        return result;
    }

    private static float HashString(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in value)
                hash = hash * 31 + c;
            return Math.Abs(hash) % 1_000_000;
        }
    }
}
