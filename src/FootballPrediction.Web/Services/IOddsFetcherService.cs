using FootballPrediction.Web.Models;

namespace FootballPrediction.Web.Services;

public interface IOddsFetcherService
{
    /// <summary>
    /// Fetch live odds for a specific match (home team vs away team) in a league.
    /// League should be in The Odds API format (e.g. "soccer_epl", "soccer_spain_la_liga").
    /// Returns populated FetchedOdds or null if not found.
    /// </summary>
    Task<FetchedOdds?> FetchOddsAsync(string league, string homeTeam, string awayTeam);
}
