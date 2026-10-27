using FootballPrediction.Domain.Entities;

namespace FootballPrediction.Application.Interfaces;

/// <summary>Cotes 1X2 d'un match à venir, par bookmaker (clés : Bet365, Pinnacle, WilliamHill, Autre).</summary>
public sealed record ExternalMatchOdds(
    string HomeTeam, string AwayTeam, DateTime CommenceTime, IReadOnlyDictionary<string, Odds> Bookmakers);

/// <summary>
/// Source de cotes pour les matchs à venir d'un championnat (ex. The Odds API).
/// Lève <see cref="FootballDataException"/> avec un message lisible en cas d'échec.
/// </summary>
public interface IMatchOddsSource
{
    Task<IReadOnlyList<ExternalMatchOdds>> GetUpcomingOddsAsync(string leagueCode, CancellationToken ct = default);
}
