using FootballPrediction.Domain.Entities;
using FootballPrediction.Domain.Enums;
using FootballPrediction.ML.DataModels;

namespace FootballPrediction.ML.FeatureEngineering;

public static class FeatureEngineer
{
    /// <summary>
    /// Build features from matches. When binaryMode is true, draws (Result=X) are filtered out
    /// and Label is restricted to "1" (HomeWin) or "2" (AwayWin).
    /// </summary>
    public static List<MatchData> BuildFeatures(IReadOnlyList<Match> matches, bool binaryMode = false)
    {
        var sorted = matches.OrderBy(m => m.Date).ToList();
        var result = new List<MatchData>(sorted.Count);

        var coachMatchCounts = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in sorted)
        {
            if (match.Result == null) continue;
            if (match.HomeGoals == null || match.AwayGoals == null) continue;

            // In binary mode, skip draws
            if (binaryMode && match.Result == MatchResult.Draw) continue;

            var homeForm = FormCalculator.CalculateForm(match.HomeTeam, match.Date, sorted);
            var awayForm = FormCalculator.CalculateForm(match.AwayTeam, match.Date, sorted);

            var (bet365Home, bet365Draw, bet365Away) = (0.33, 0.34, 0.33);
            if (match.BookmakerOdds.TryGetValue("Bet365", out var bet365))
            {
                (bet365Home, bet365Draw, bet365Away) = OddsNormalizer.Normalize(
                    (double)bet365.HomeWin, (double)bet365.Draw, (double)bet365.AwayWin);
            }

            var (pinHome, pinDraw, pinAway) = (0.33, 0.34, 0.33);
            if (match.BookmakerOdds.TryGetValue("Pinnacle", out var pinnacle))
            {
                (pinHome, pinDraw, pinAway) = OddsNormalizer.Normalize(
                    (double)pinnacle.HomeWin, (double)pinnacle.Draw, (double)pinnacle.AwayWin);
            }

            var homeTenure = GetCoachTenure(match.HomeTeam, match.HomeCoach, match.Date, coachMatchCounts);
            var awayTenure = GetCoachTenure(match.AwayTeam, match.AwayCoach, match.Date, coachMatchCounts);

            result.Add(new MatchData
            {
                League = match.League,
                Season = match.Season,
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam,
                HomeCoach = match.HomeCoach ?? "Unknown",
                AwayCoach = match.AwayCoach ?? "Unknown",
                Bet365HomeProb = (float)bet365Home,
                Bet365DrawProb = (float)bet365Draw,
                Bet365AwayProb = (float)bet365Away,
                PinnacleHomeProb = (float)pinHome,
                PinnacleDrawProb = (float)pinDraw,
                PinnacleAwayProb = (float)pinAway,
                HomeForm5 = (float)homeForm.PointsPerGame,
                AwayForm5 = (float)awayForm.PointsPerGame,
                HomeGoalsForAvg = (float)homeForm.GoalsForAvg,
                AwayGoalsForAvg = (float)awayForm.GoalsForAvg,
                HomeGoalsAgainstAvg = (float)homeForm.GoalsAgainstAvg,
                AwayGoalsAgainstAvg = (float)awayForm.GoalsAgainstAvg,
                FormDiff = (float)FormCalculator.FormDiff(homeForm, awayForm),
                HomeCoachTenure = (float)homeTenure,
                AwayCoachTenure = (float)awayTenure,
                Label = match.Result.Value.ToDisplayString(),
                HomeWin = match.Result == MatchResult.HomeWin
            });
        }

        return result;
    }

    private static double GetCoachTenure(
        string team,
        string? coach,
        DateTime matchDate,
        Dictionary<string, Dictionary<string, int>> coachMatchCounts)
    {
        if (string.IsNullOrEmpty(coach) || coach == "Unknown")
            return 0;

        if (!coachMatchCounts.ContainsKey(team))
            coachMatchCounts[team] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (!coachMatchCounts[team].ContainsKey(coach))
        {
            coachMatchCounts[team][coach] = 1;
            return 0;
        }

        var currentCount = coachMatchCounts[team][coach];
        coachMatchCounts[team][coach] = currentCount + 1;
        return currentCount;
    }
}
