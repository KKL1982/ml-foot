using FootballPrediction.Domain.Entities;

namespace FootballPrediction.Application.Interfaces;

/// <summary>Entraîneur actuel d'une équipe tel que renvoyé par la source externe.</summary>
public sealed record ExternalTeamCoach(string Team, string? Coach);

/// <summary>
/// Source externe de données de football (matchs à venir, entraîneurs actuels).
/// Les noms d'équipes sont ceux de la source (non normalisés). Les codes de
/// championnat sont ceux du projet (PL, LL, L1, BL).
/// </summary>
public interface IFootballDataClient
{
    /// <summary>Matchs d'un championnat entre deux dates (sans résultat ni score).</summary>
    Task<IReadOnlyList<Match>> GetMatchesAsync(string leagueCode, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>Entraîneur actuel de chaque équipe d'un championnat.</summary>
    Task<IReadOnlyList<ExternalTeamCoach>> GetTeamCoachesAsync(string leagueCode, CancellationToken ct = default);
}

/// <summary>Erreur lisible par l'utilisateur (clé absente, quota, API injoignable...).</summary>
public sealed class FootballDataException : Exception
{
    public FootballDataException(string message, Exception? inner = null) : base(message, inner) { }
}
