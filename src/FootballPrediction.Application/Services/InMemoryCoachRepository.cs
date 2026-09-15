using FootballPrediction.Application.Interfaces;
using FootballPrediction.Domain.Entities;

namespace FootballPrediction.Application.Services;

/// <summary>
/// Implémentation mémoire de <see cref="ICoachRepository"/>. Conserve
/// l'historique des entraîneurs par équipe dans un dictionnaire. Suffisante
/// pour le CLI, les tests et une session Web ; peut être remplacée par une
/// implémentation persistante (JSON/BDD) sans changer l'API.
/// </summary>
public class InMemoryCoachRepository : ICoachRepository
{
    private readonly Dictionary<string, List<CoachAssignment>> _byTeam =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _lock = new();

    public string? GetCoach(string team, DateTime asOfDate)
    {
        lock (_lock)
        {
            if (!_byTeam.TryGetValue(team, out var list))
                return null;

            CoachAssignment? best = null;
            foreach (var a in list)
            {
                if (a.EffectiveFrom <= asOfDate && (best == null || a.EffectiveFrom > best.EffectiveFrom))
                    best = a;
            }
            return best?.Coach;
        }
    }

    public IReadOnlyList<CoachAssignment> GetHistory(string team)
    {
        lock (_lock)
        {
            if (!_byTeam.TryGetValue(team, out var list))
                return Array.Empty<CoachAssignment>();
            return list.OrderBy(a => a.EffectiveFrom).ToList();
        }
    }

    public void Upsert(CoachAssignment assignment)
    {
        lock (_lock)
        {
            if (!_byTeam.TryGetValue(assignment.Team, out var list))
            {
                _byTeam[assignment.Team] = new List<CoachAssignment> { assignment };
                return;
            }

            // Idempotence : même team + coach + date d'effet → on ne duplique pas.
            var existing = list.FirstOrDefault(a =>
                string.Equals(a.Coach, assignment.Coach, StringComparison.OrdinalIgnoreCase)
                && a.EffectiveFrom == assignment.EffectiveFrom);
            if (existing != null)
            {
                existing.League = assignment.League ?? existing.League;
                return;
            }

            list.Add(assignment);
            // Si on remplace le coach actif (nouveau coach plus récent que tous),
            // on garde l'historique — GetCoach retourne le plus récent.
        }
    }

    public IReadOnlyList<string> GetTeams()
    {
        lock (_lock)
        {
            return _byTeam.Keys.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
