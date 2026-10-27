using System.Net;
using System.Text.Json;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Application.Services;
using FootballPrediction.Domain.Entities;
using FootballPrediction.Web.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FootballPrediction.Web.Services;

/// <summary>
/// Client de l'API football-data.org (v4). Fournit les matchs d'une période et
/// l'entraîneur actuel de chaque équipe. Les erreurs sont converties en
/// <see cref="FootballDataException"/> avec un message lisible.
/// </summary>
public class FootballDataClient : IFootballDataClient
{
    // Code projet -> code football-data.org
    private static readonly Dictionary<string, string> CompetitionCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PL"] = "PL", ["LL"] = "PD", ["L1"] = "FL1", ["BL"] = "BL1",
    };

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly FootballDataOptions _options;
    private readonly ILogger<FootballDataClient> _logger;

    public FootballDataClient(
        HttpClient http, IMemoryCache cache, IOptions<FootballDataOptions> options, ILogger<FootballDataClient> logger)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Match>> GetMatchesAsync(
        string leagueCode, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var competition = ResolveCompetition(leagueCode, out var leagueName);
        var url = $"{_options.BaseUrl}/competitions/{competition}/matches?dateFrom={from:yyyy-MM-dd}&dateTo={to:yyyy-MM-dd}";

        var response = await GetAsync<MatchesResponse>(url, ct);
        var matches = new List<Match>();
        foreach (var m in response.Matches ?? new())
        {
            var home = m.HomeTeam?.Name ?? m.HomeTeam?.ShortName;
            var away = m.AwayTeam?.Name ?? m.AwayTeam?.ShortName;
            if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away))
                continue; // match dont une équipe n'est pas encore connue (phases à élimination)

            var date = m.UtcDate.ToUniversalTime().Date;
            matches.Add(new Match
            {
                Date = date,
                League = leagueName,
                Season = SeasonFor(date),
                HomeTeam = home,
                AwayTeam = away,
            });
        }
        return matches;
    }

    public async Task<IReadOnlyList<ExternalTeamCoach>> GetTeamCoachesAsync(string leagueCode, CancellationToken ct = default)
    {
        var competition = ResolveCompetition(leagueCode, out _);
        var url = $"{_options.BaseUrl}/competitions/{competition}/teams";

        var response = await GetAsync<TeamsResponse>(url, ct);
        return (response.Teams ?? new())
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .Select(t => new ExternalTeamCoach(t.Name!, t.Coach?.Name))
            .ToList();
    }

    /// <summary>Saison européenne : août 2026 -> « 2026-2027 », mars 2027 -> « 2026-2027 ».</summary>
    public static string SeasonFor(DateTime date)
    {
        var start = date.Month >= 7 ? date.Year : date.Year - 1;
        return $"{start}-{start + 1}";
    }

    private static string ResolveCompetition(string leagueCode, out string leagueName)
    {
        var championship = ChampionshipService.DefaultChampionships
            .FirstOrDefault(c => string.Equals(c.Code, leagueCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new FootballDataException($"Championnat inconnu : {leagueCode}.");
        leagueName = championship.Name;
        return CompetitionCodes[championship.Code];
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken ct) where T : new()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new FootballDataException(
                "Clé football-data.org non configurée (section FootballData:ApiKey de appsettings.Production.json).");

        if (_cache.TryGetValue(url, out T? cached) && cached != null)
            return cached;

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Auth-Token", _options.ApiKey);
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "football-data.org injoignable : {Url}", url);
            throw new FootballDataException("football-data.org est injoignable. Vérifiez la connexion du serveur.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("football-data.org a répondu {Status} pour {Url}", (int)response.StatusCode, url);
                throw new FootballDataException(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "Clé refusée ou championnat non inclus dans votre offre football-data.org.",
                    HttpStatusCode.TooManyRequests =>
                        "Limite de requêtes football-data.org atteinte. Réessayez dans une minute.",
                    _ => $"football-data.org a répondu {(int)response.StatusCode}."
                });
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            T? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<T>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new FootballDataException("Réponse football-data.org illisible.", ex);
            }

            parsed ??= new T();
            _cache.Set(url, parsed, TimeSpan.FromMinutes(Math.Max(0, _options.CacheMinutes)));
            return parsed;
        }
    }

    private sealed class MatchesResponse { public List<ApiMatch>? Matches { get; set; } }
    private sealed class ApiMatch
    {
        public DateTime UtcDate { get; set; }
        public ApiTeam? HomeTeam { get; set; }
        public ApiTeam? AwayTeam { get; set; }
    }
    private sealed class TeamsResponse { public List<ApiTeam>? Teams { get; set; } }
    private sealed class ApiTeam
    {
        public string? Name { get; set; }
        public string? ShortName { get; set; }
        public ApiCoach? Coach { get; set; }
    }
    private sealed class ApiCoach { public string? Name { get; set; } }
}
