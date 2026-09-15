using System.Text.Json;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Domain.Entities;

namespace FootballPrediction.Application.Services;

/// <summary>
/// Implémentation de <see cref="ICoachRepository"/> persistée dans un fichier
/// JSON. Permet de conserver les mises à jour d'entraîneurs entre deux
/// exécutions du CLI (chargé au démarrage, réécrit à chaque Upsert).
/// </summary>
public sealed class JsonCoachRepository : ICoachRepository
{
    private readonly string _filePath;
    private readonly Dictionary<string, List<CoachAssignment>> _byTeam =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public JsonCoachRepository(string filePath)
    {
        _filePath = filePath;
        Load();
    }

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
            }
            else
            {
                var existing = list.FirstOrDefault(a =>
                    string.Equals(a.Coach, assignment.Coach, StringComparison.OrdinalIgnoreCase)
                    && a.EffectiveFrom == assignment.EffectiveFrom);
                if (existing == null)
                    list.Add(assignment);
            }
            Save();
        }
    }

    public IReadOnlyList<string> GetTeams()
    {
        lock (_lock)
        {
            return _byTeam.Keys.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
            return;
        try
        {
            var rows = JsonSerializer.Deserialize<List<CoachAssignment>>(File.ReadAllText(_filePath));
            if (rows == null)
                return;
            foreach (var a in rows)
            {
                var team = a.Team.Trim();
                if (string.IsNullOrEmpty(team)) continue;
                if (!_byTeam.ContainsKey(team))
                    _byTeam[team] = new List<CoachAssignment>();
                _byTeam[team].Add(a);
            }
        }
        catch (JsonException)
        {
            // Fichier corrompu ou vide → on ignore et on repart propre.
        }
    }

    private void Save()
    {
        try
        {
            var list = _byTeam.Values.SelectMany(v => v).ToList();
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException)
        {
            // Échec d'écriture : on ne fait pas échouer la mise en mémoire.
        }
    }
}
