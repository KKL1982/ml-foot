using FootballPrediction.Domain.Entities;
using FootballPrediction.Domain.Enums;

namespace FootballPrediction.ML.FeatureEngineering;

public static class FormCalculator
{
    public record TeamForm(
        int MatchesPlayed,
        int Wins,
        int Draws,
        int Losses,
        double GoalsForAvg,
        double GoalsAgainstAvg,
        double GoalDifference,
        double PointsPerGame);

    public static TeamForm CalculateForm(
        string team,
        DateTime beforeDate,
        IReadOnlyList<Match> historicalMatches,
        int windowSize = 5)
    {
        var recentMatches = historicalMatches
            .Where(m => m.Date < beforeDate &&
                        (m.HomeTeam.Equals(team, StringComparison.OrdinalIgnoreCase) ||
                         m.AwayTeam.Equals(team, StringComparison.OrdinalIgnoreCase)) &&
                        m.HomeGoals.HasValue && m.AwayGoals.HasValue)
            .OrderByDescending(m => m.Date)
            .Take(windowSize)
            .ToList();

        if (recentMatches.Count == 0)
            return new TeamForm(0, 0, 0, 0, 0, 0, 0, 0);

        int wins = 0, draws = 0, losses = 0;
        double goalsFor = 0, goalsAgainst = 0;

        foreach (var match in recentMatches)
        {
            bool isHome = match.HomeTeam.Equals(team, StringComparison.OrdinalIgnoreCase);
            var gf = isHome ? match.HomeGoals!.Value : match.AwayGoals!.Value;
            var ga = isHome ? match.AwayGoals!.Value : match.HomeGoals!.Value;

            goalsFor += gf;
            goalsAgainst += ga;

            if (gf > ga) wins++;
            else if (gf == ga) draws++;
            else losses++;
        }

        var count = recentMatches.Count;
        return new TeamForm(
            count,
            wins,
            draws,
            losses,
            goalsFor / count,
            goalsAgainst / count,
            (goalsFor - goalsAgainst) / count,
            (wins * 3.0 + draws) / count);
    }

    public static double FormDiff(TeamForm homeForm, TeamForm awayForm)
    {
        return homeForm.PointsPerGame - awayForm.PointsPerGame;
    }
}
