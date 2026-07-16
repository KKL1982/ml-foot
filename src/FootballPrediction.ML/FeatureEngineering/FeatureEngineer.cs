using FootballPrediction.Domain.Entities;
using FootballPrediction.Domain.Enums;
using FootballPrediction.ML.DataModels;

namespace FootballPrediction.ML.FeatureEngineering;

public static class FeatureEngineer
{
    public static List<MatchData> BuildFeatures(IReadOnlyList<Match> matches)
    {
        var sorted = matches.OrderBy(m => m.Date).ToList();
        var result = new List<MatchData>(sorted.Count);

        // Track coach tenure per team
        var coachStartDates = new Dictionary<string, Dictionary<string, DateTime>>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in sorted)
        {
            if (match.Result == null) continue;
            if (match.HomeGoals == null || match.AwayGoals == null) continue;

            var homeForm = FormCalculator.CalculateForm(match.HomeTeam, match.Date, sorted);
            var awayForm = FormCalculator.CalculateForm(match.AwayTeam, match.Date, sorted);

            // Bet365 probabilities
            var (bet365Home, bet365Draw, bet365Away) = (0.33, 0.34, 0.33);
            if (match.BookmakerOdds.TryGetValue("Bet365", out var bet365))
            {
                (bet365Home, bet365Draw, bet365Away) = OddsNormalizer.Normalize(
                    (double)bet365.HomeWin, (double)bet365.Draw, (double)bet365.AwayWin);
            }

            // Pinnacle probabilities
            var (pinHome, pinDraw, pinAway) = (0.33, 0.34, 0.33);
            if (match.BookmakerOdds.TryGetValue("Pinnacle", out var pinnacle))
            {
                (pinHome, pinDraw, pinAway) = OddsNormalizer.Normalize(
                    (double)pinnacle.HomeWin, (double)pinnacle.Draw, (double)pinnacle.AwayWin);
            }

            // Coach tenure (number of matches this coach has been with the team before this match)
            var homeTenure = GetCoachTenure(match.HomeTeam, match.HomeCoach, match.Date, coachStartDates);
            var awayTenure = GetCoachTenure(match.AwayTeam, match.AwayCoach, match.Date, coachStartDates);

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
                Label = match.Result.Value.ToDisplayString()
            });
        }

        return result;
    }

    private static double GetCoachTenure(
        string team,
        string? coach,
        DateTime matchDate,
        Dictionary<string, Dictionary<string, DateTime>> coachStartDates)
    {
        if (string.IsNullOrEmpty(coach) || coach == "Unknown")
            return 0;

        // Ensure team entry exists
        if (!coachStartDates.ContainsKey(team))
            coachStartDates[team] = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        // If this is the first time we see this coach for this team, record the start
        if (!coachStartDates[team].ContainsKey(coach))
        {
            coachStartDates[team][coach] = matchDate;
            return 0; // First match, tenure = 0
        }

        // Count matches this coach has had with this team before this match
        var startDate = coachStartDates[team][coach];
        // We approximate tenure as number of matches seen so far for this coach
        // A simpler approach: count how many times we've seen this coach before
        return coachStartDates[team].Where(kvp => kvp.Key == coach).Count();
    }
}
