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
    /// <summary>Nombre de matchs pour lesquels des cotes ont été trouvées.</summary>
    public int MatchesWithOdds { get; init; }
    /// <summary>Vrai si une source de cotes est configurée.</summary>
    public bool OddsEnabled { get; init; }
    /// <summary>Problèmes de cotes (clé absente, quota...) : la prédiction continue sans.</summary>
    public IReadOnlyList<string> OddsWarnings { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Récupère les matchs d'une période depuis la source externe, normalise les
/// noms d'équipes et enregistre le résultat dans un CSV réutilisable.
/// </summary>
public sealed class MatchFetchService
{
    private readonly IFootballDataClient _client;
    private readonly MatchFileStore _store;
    private readonly IMatchOddsSource? _oddsSource;

    public MatchFetchService(IFootballDataClient client, MatchFileStore store, IMatchOddsSource? oddsSource = null)
    {
        _client = client;
        _store = store;
        _oddsSource = oddsSource;
    }

    public async Task<MatchFetchResult> FetchAsync(
        IEnumerable<string> leagueCodes, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var codes = leagueCodes.Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();

        var matches = new List<Match>();
        var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var oddsWarnings = new List<string>();
        var withOdds = 0;

        foreach (var code in codes)
        {
            var leagueMatches = new List<Match>();
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
                    leagueMatches.Add(m);
                }
            }
            catch (FootballDataException ex)
            {
                errors.Add($"{code} : {ex.Message}");
                continue;
            }

            if (_oddsSource != null && leagueMatches.Count > 0)
            {
                try
                {
                    var odds = await _oddsSource.GetUpcomingOddsAsync(code, ct);
                    withOdds += AttachOdds(leagueMatches, odds);
                }
                catch (FootballDataException ex)
                {
                    oddsWarnings.Add($"Cotes {code} : {ex.Message}");
                }
            }
            matches.AddRange(leagueMatches);
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
            Errors = errors,
            MatchesWithOdds = withOdds,
            OddsEnabled = _oddsSource != null,
            OddsWarnings = oddsWarnings
        };
    }

    /// <summary>Associe chaque match à ses cotes (mêmes équipes, date à ±1 jour). Retourne le nombre de matchs servis.</summary>
    public static int AttachOdds(IEnumerable<Match> matches, IReadOnlyList<ExternalMatchOdds> odds)
    {
        var index = odds
            .Select(o => (Odds: o, Home: TeamNameMapper.Map(o.HomeTeam).Name, Away: TeamNameMapper.Map(o.AwayTeam).Name))
            .ToList();
        var attached = 0;

        foreach (var m in matches)
        {
            var hit = index.FirstOrDefault(x =>
                SameTeam(x.Home, m.HomeTeam) && SameTeam(x.Away, m.AwayTeam)
                && Math.Abs((x.Odds.CommenceTime.ToUniversalTime().Date - m.Date.Date).TotalDays) <= 1);
            if (hit.Odds == null || hit.Odds.Bookmakers.Count == 0)
                continue;
            foreach (var kv in hit.Odds.Bookmakers)
                m.BookmakerOdds[kv.Key] = kv.Value;
            attached++;
        }
        return attached;
    }

    private static bool SameTeam(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        var x = new string(a.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var y = new string(b.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return x.Length >= 5 && y.Length >= 5 && (x.Contains(y) || y.Contains(x));
    }
}
