namespace FootballPrediction.Domain.Enums;

public enum MatchResult
{
    HomeWin,
    Draw,
    AwayWin
}

public static class MatchResultExtensions
{
    public static string ToDisplayString(this MatchResult result) => result switch
    {
        MatchResult.HomeWin => "1",
        MatchResult.Draw => "X",
        MatchResult.AwayWin => "2",
        _ => throw new ArgumentOutOfRangeException(nameof(result))
    };

    public static MatchResult FromScore(int homeGoals, int awayGoals) =>
        homeGoals > awayGoals ? MatchResult.HomeWin :
        homeGoals < awayGoals ? MatchResult.AwayWin :
        MatchResult.Draw;
}
