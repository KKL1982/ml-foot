using FootballPrediction.Application.Interfaces;
using FootballPrediction.Domain.Entities;
using FootballPrediction.ML.DataModels;
using FootballPrediction.ML.FeatureEngineering;

namespace FootballPrediction.Cli;

/// <summary>
/// Construit un vecteur de features (<see cref="MatchData"/>) pour un match à
/// PRÉDIRE (non joué). Contrairement à FeatureEngineer.BuildFeatures (qui n'admet
/// que des matchs joués avec résultat), ce builder accepte des matchs à venir et
/// applique les coachs à jour via un ICoachRepository quand il est fourni.
/// </summary>
public static class MatchDataBuilder
{
    public static MatchData Build(Match match, ICoachRepository? coaches, DateTime? asOfDate = null)
    {
        var date = asOfDate ?? match.Date;
        var homeCoach = coaches?.GetCoach(match.HomeTeam, date) ?? match.HomeCoach ?? "Unknown";
        var awayCoach = coaches?.GetCoach(match.AwayTeam, date) ?? match.AwayCoach ?? "Unknown";

        float bet365Home = 0.33f, bet365Draw = 0.34f, bet365Away = 0.33f;
        if (match.BookmakerOdds.TryGetValue("Bet365", out var b365) && b365.HomeWin > 1)
        {
            var h = OddsNormalizer.Normalize((double)b365.HomeWin, (double)b365.Draw, (double)b365.AwayWin);
            bet365Home = (float)h.Item1; bet365Draw = (float)h.Item2; bet365Away = (float)h.Item3;
        }

        float pinHome = 0.33f, pinDraw = 0.34f, pinAway = 0.33f;
        if (match.BookmakerOdds.TryGetValue("Pinnacle", out var pin) && pin.HomeWin > 1)
        {
            var pinNorm = OddsNormalizer.Normalize((double)pin.HomeWin, (double)pin.Draw, (double)pin.AwayWin);
            pinHome = (float)pinNorm.Item1; pinDraw = (float)pinNorm.Item2; pinAway = (float)pinNorm.Item3;
        }

        return new MatchData
        {
            League = match.League,
            Season = match.Season,
            HomeTeam = match.HomeTeam,
            AwayTeam = match.AwayTeam,
            HomeCoach = homeCoach,
            AwayCoach = awayCoach,
            Bet365HomeProb = bet365Home,
            Bet365DrawProb = bet365Draw,
            Bet365AwayProb = bet365Away,
            PinnacleHomeProb = pinHome,
            PinnacleDrawProb = pinDraw,
            PinnacleAwayProb = pinAway,
            HomeForm5 = 0, AwayForm5 = 0,
            HomeGoalsForAvg = 0, AwayGoalsForAvg = 0,
            HomeGoalsAgainstAvg = 0, AwayGoalsAgainstAvg = 0,
            FormDiff = 0, HomeCoachTenure = 0, AwayCoachTenure = 0,
            Label = string.Empty,
            HomeWin = false
        };
    }
}
