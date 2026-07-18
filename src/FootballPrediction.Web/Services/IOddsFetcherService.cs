using FootballPrediction.Web.Models;

namespace FootballPrediction.Web.Services;

public interface IOddsFetcherService
{
    /// <summary>
    /// Fetch live odds for a specific match. Never returns null — check <c>result.Error</c> for failures.
    /// </summary>
    Task<FetchedOdds> FetchOddsAsync(string league, string homeTeam, string awayTeam);
}
