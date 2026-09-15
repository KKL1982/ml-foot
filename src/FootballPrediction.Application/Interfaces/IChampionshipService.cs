using FootballPrediction.Domain.Entities;

namespace FootballPrediction.Application.Interfaces;

/// <summary>
/// Fournit l'accès aux championnats supportés, à leurs équipes et au chargement
/// des matchs d'un championnat (par plage de dates). Le chargement réhydrate
/// les matchs depuis une source CSV et applique les entraîneurs connus à jour.
/// </summary>
public interface IChampionshipService
{
    /// <summary>Retourne les championnats supportés (par défaut PL/LL/L1/BL).</summary>
    IReadOnlyList<Championship> GetChampionships();

    /// <summary>Retourne l'équipe d'un championnat à partir de son nom exact (insensible à la casse).</summary>
    string? ResolveTeam(string championship, string teamName);

    /// <summary>Liste (sans doublon, triée) des noms d'équipes d'un championnat.</summary>
    IReadOnlyList<string> GetTeams(string championship);

    /// <summary>
    /// Charge les matchs joués (ou prévus) d'un championnat entre deux dates,
    /// triés chronologiquement. Championnat inconnu → liste vide (ne lève pas).
    /// </summary>
    Task<IReadOnlyList<Match>> LoadMatchesAsync(string championship, DateTime from, DateTime to);

    /// <summary>
    /// Charge tous les matchs d'un championnat, toutes dates confondues.
    /// </summary>
    Task<IReadOnlyList<Match>> LoadMatchesAsync(string championship);
}
