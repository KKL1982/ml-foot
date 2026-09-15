using FootballPrediction.Application.Interfaces;
using FootballPrediction.Domain.Entities;

namespace FootballPrediction.Application.Services;

/// <summary>
/// Service de championnats : liste les championnats supportés, leurs équipes et
/// charge les matchs d'un championnat (par plage de dates). Les données brutes
/// proviennent d'un fichier CSV (le même format que le CSV de référence du repo).
/// Le nom de championnat est mis en correspondance de façon insensible à la casse.
/// </summary>
public class ChampionshipService : IChampionshipService
{
    private readonly ICsvParser _csvParser;
    private readonly string _dataFilePath;
    private readonly object _cacheLock = new();

    private IReadOnlyList<Championship>? _championshipsCache;
    private IReadOnlyList<Match>? _matchesCache;

    /// <summary>Les 4 championnats supportés par défaut (ceux présents dans les données).</summary>
    public static readonly Championship[] DefaultChampionships =
    {
        new("PL", "Premier League"),
        new("LL", "LaLiga"),
        new("L1", "Ligue 1"),
        new("BL", "Bundesliga"),
    };

    public ChampionshipService(ICsvParser csvParser, string dataFilePath)
    {
        _csvParser = csvParser;
        _dataFilePath = dataFilePath;
    }

    public IReadOnlyList<Championship> GetChampionships()
    {
        if (_championshipsCache != null)
            return _championshipsCache;
        lock (_cacheLock)
        {
            if (_championshipsCache != null)
                return _championshipsCache;
            _championshipsCache = DefaultChampionships;
            return _championshipsCache;
        }
    }

    public string? ResolveTeam(string championship, string teamName)
    {
        var teams = GetTeams(championship);
        return teams.FirstOrDefault(t => string.Equals(t, teamName, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> GetTeams(string championship)
    {
        var matches = GetAllMatches();
        var name = ResolveChampionshipName(championship);
        if (name == null)
            return Array.Empty<string>();

        return matches
            .Where(m => string.Equals(m.League, name, StringComparison.OrdinalIgnoreCase))
            .SelectMany(m => new[] { m.HomeTeam, m.AwayTeam })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<IReadOnlyList<Match>> LoadMatchesAsync(string championship, DateTime from, DateTime to)
    {
        var matches = GetAllMatches();
        var name = ResolveChampionshipName(championship);
        if (name == null)
            return Task.FromResult<IReadOnlyList<Match>>(Array.Empty<Match>());

        return Task.FromResult<IReadOnlyList<Match>>(matches
            .Where(m => string.Equals(m.League, name, StringComparison.OrdinalIgnoreCase)
                        && m.Date >= from && m.Date <= to)
            .OrderBy(m => m.Date)
            .ToList());
    }

    public Task<IReadOnlyList<Match>> LoadMatchesAsync(string championship)
    {
        var matches = GetAllMatches();
        var name = ResolveChampionshipName(championship);
        if (name == null)
            return Task.FromResult<IReadOnlyList<Match>>(Array.Empty<Match>());

        return Task.FromResult<IReadOnlyList<Match>>(matches
            .Where(m => string.Equals(m.League, name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.Date)
            .ToList());
    }

    private string? ResolveChampionshipName(string championship)
    {
        if (string.IsNullOrWhiteSpace(championship))
            return null;

        foreach (var c in GetChampionships())
        {
            if (string.Equals(c.Code, championship, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Name, championship, StringComparison.OrdinalIgnoreCase))
                return c.Name;
        }
        return null;
    }

    private IReadOnlyList<Match> GetAllMatches()
    {
        if (_matchesCache != null)
            return _matchesCache;
        lock (_cacheLock)
        {
            if (_matchesCache != null)
                return _matchesCache;
            var matches = Task.Run(() => _csvParser.ParseMatchesAsync(_dataFilePath)).GetAwaiter().GetResult();
            _matchesCache = matches;
            return _matchesCache;
        }
    }
}
