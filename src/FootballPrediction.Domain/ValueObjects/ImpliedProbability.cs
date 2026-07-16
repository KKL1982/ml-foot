namespace FootballPrediction.Domain.ValueObjects;

public class ImpliedProbability
{
    public double HomeWin { get; init; }
    public double Draw { get; init; }
    public double AwayWin { get; init; }

    public static ImpliedProbability FromOdds(double homeOdds, double drawOdds, double awayOdds)
    {
        if (homeOdds <= 0 || drawOdds <= 0 || awayOdds <= 0)
            throw new ArgumentException("Odds must be strictly positive.");

        var rawHome = 1.0 / homeOdds;
        var rawDraw = 1.0 / drawOdds;
        var rawAway = 1.0 / awayOdds;
        var total = rawHome + rawDraw + rawAway;

        return new ImpliedProbability
        {
            HomeWin = rawHome / total,
            Draw = rawDraw / total,
            AwayWin = rawAway / total
        };
    }

    public double Sum => HomeWin + Draw + AwayWin;
}
