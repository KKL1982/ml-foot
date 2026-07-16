namespace FootballPrediction.ML.FeatureEngineering;

public static class OddsNormalizer
{
    public static (double homeProb, double drawProb, double awayProb) Normalize(
        double homeOdds, double drawOdds, double awayOdds)
    {
        if (homeOdds <= 0 || drawOdds <= 0 || awayOdds <= 0)
            throw new ArgumentException("Odds must be strictly positive.");

        var rawHome = 1.0 / homeOdds;
        var rawDraw = 1.0 / drawOdds;
        var rawAway = 1.0 / awayOdds;
        var total = rawHome + rawDraw + rawAway;

        return (rawHome / total, rawDraw / total, rawAway / total);
    }

    public static double HomeVsAwayProbabilityGap(double homeOdds, double awayOdds)
    {
        var (homeP, _, awayP) = Normalize(homeOdds, 3.0, awayOdds);
        return homeP - awayP;
    }
}
