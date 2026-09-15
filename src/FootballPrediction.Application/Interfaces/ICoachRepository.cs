using FootballPrediction.Domain.Entities;

namespace FootballPrediction.Application.Interfaces;

/// <summary>
/// Persistance de l'historique des entraîneurs par équipe. Permet de retrouver
/// le coach actif à une date donnée et d'appliquer des mises à jour (un coach
/// qui change de poste).
/// </summary>
public interface ICoachRepository
{
    /// <summary>
    /// Retourne le coach actif pour une équipe à une date donnée, ou null si
    /// aucun coach connu pour cette équipe à cette date.
    /// </summary>
    string? GetCoach(string team, DateTime asOfDate);

    /// <summary>Historique complet des affectations d'une équipe, trié par date d'effet croissante.</summary>
    IReadOnlyList<CoachAssignment> GetHistory(string team);

    /// <summary>
    /// Applique (ou met à jour) une affectation d'entraîneur. Idempotente : une
    /// affectation strictement identique (même team+coach+date) ne crée pas de doublon.
    /// </summary>
    void Upsert(CoachAssignment assignment);

    /// <summary>Liste de toutes les équipes connues du repository.</summary>
    IReadOnlyList<string> GetTeams();
}
