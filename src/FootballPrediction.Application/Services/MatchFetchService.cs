using FootballPrediction.Application.Interfaces;
using FootballPrediction.Domain.Entities;

namespace FootballPrediction.Application.Services;

public sealed class MatchFetchResult
{
    /// <summary>Nom du CSV créé (null si aucun match n'a été récupéré).</summary>
    public string? FileName { get; init; }
    public IReadOnlyList<Match> Matches { get; init; } = Array.Empty<Match>();
    /// <summary>Équipes absentes de la table de correspondance (utilisées avec leur nom nettoyé).</summary>
    public IReadOnlyList<string> UnknownTeams { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Récupère les matchs d'une période depuis la source externe, normalise les
/// noms d'équipes et enregistre le résultat dans un CSV réutilisable.
/// </summary>
public sealed class MatchFetchService
{
    private readonly IFootballDataClient _client;
    private readonly MatchFileStore _store;

    public MatchFetchService(IFootballDataClient client, MatchFileStore store)
    {
        _client = client;
        _store = store;
    }

    public async Task<MatchFetchResult> FetchAsync(
        IEnumerable<string> leagueCodes, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var codes = leagueCodes.Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();

        var matches = new List<Match>();
        var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        foreach (var code in codes)
        {
            try
            {
                foreach (var m in await _client.GetMatchesAsync(code, from, to, ct))
                {
                    var home = TeamNameMapper.Map(m.HomeTeam);
                    var away = TeamNameMapper.Map(m.AwayTeam);
                    if (!home.Known) unknown.Add(home.Name);
                    if (!away.Known) unknown.Add(away.Name);
                    m.HomeTeam = home.Name;
                    m.AwayTeam = away.Name;
                    matches.Add(m);
                }
            }
            catch (FootballDataException ex)
            {
                errors.Add($"{code} : {ex.Message}");
            }
        }

        matches = matches.OrderBy(m => m.Date).ThenBy(m => m.League).ThenBy(m => m.HomeTeam).ToList();

        string? fileName = null;
        if (matches.Count > 0)
        {
            fileName = $"matches_{string.Join("-", codes)}_{from:yyyyMMdd}_{to:yyyyMMdd}.csv";
            await _store.SaveAsync(fileName, matches);
        }

        return new MatchFetchResult
        {
            FileName = fileName,
            Matches = matches,
            UnknownTeams = unknown.ToList(),
            Errors = errors
        };
    }
}
