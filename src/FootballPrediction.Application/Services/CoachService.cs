using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Domain.Entities;

namespace FootballPrediction.Application.Services;

/// <summary>
/// Service de mise à jour des entraîneurs des équipes des championnats
/// concernés. Applique des <see cref="CoachAssignment"/> (directement ou via
/// une demande <see cref="CoachUpdate"/>) et permet de synchroniser l'historique
/// des entraîneurs depuis un fichier CSV (ex. des mises à jour hebdomadaires).
/// </summary>
public class CoachService
{
    private readonly ICoachRepository _repository;

    public CoachService(ICoachRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Applique une mise à jour d'entraîneur pour une équipe. Si une affectation
    /// existe déjà à une date antérieure pour le même coach, elle est idempotente.
    /// </summary>
    public void UpdateCoach(string team, string coach, DateTime effectiveFrom, string? league = null)
    {
        if (string.IsNullOrWhiteSpace(team))
            return;
        if (string.IsNullOrWhiteSpace(coach))
            return;

        _repository.Upsert(new CoachAssignment(team.Trim(), coach.Trim(), effectiveFrom, league));
    }

    /// <summary>Applique une liste de mises à jour et retourne le compteur appliqué.</summary>
    public int UpdateRange(IEnumerable<CoachUpdate> updates)
    {
        var applied = 0;
        foreach (var u in updates)
        {
            if (string.IsNullOrWhiteSpace(u.Team) || string.IsNullOrWhiteSpace(u.Coach))
                continue;
            _repository.Upsert(new CoachAssignment(u.Team.Trim(), u.Coach.Trim(), u.EffectiveFrom, u.League));
            applied++;
        }
        return applied;
    }

    /// <summary>Retourne le coach actif à une date donnée (ou null).</summary>
    public string? GetCoach(string team, DateTime asOfDate) => _repository.GetCoach(team, asOfDate);

    /// <summary>Retourne l'historique complet des entraîneurs d'une équipe.</summary>
    public IReadOnlyList<CoachAssignment> GetHistory(string team) => _repository.GetHistory(team);
}
