using System.Text.Json;
using FootballPrediction.Web.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FootballPrediction.Web.Services;

public class OddsFetcherService : IOddsFetcherService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly OddsApiOptions _options;
    private readonly ILogger<OddsFetcherService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Dictionary<string, string> LeagueMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Premier League"] = "soccer_epl",
        ["LaLiga"] = "soccer_spain_la_liga",
        ["Bundesliga"] = "soccer_germany_bundesliga",
        ["Ligue 1"] = "soccer_france_ligue_one",
        ["Serie A"] = "soccer_italy_serie_a",
        ["EPL"] = "soccer_epl",
    };

    public OddsFetcherService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<OddsApiOptions> options,
        ILogger<OddsFetcherService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FetchedOdds> FetchOddsAsync(string league, string homeTeam, string awayTeam)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _logger.LogWarning("Odds API key not configured");
                return Error("Live odds not configured — add your API key in appsettings.json (OddsApi:ApiKey)");
            }

            var sportKey = MapLeague(league);
            if (sportKey == null)
            {
                return Error($"League '{league}' not yet supported. Available: {string.Join(", ", LeagueMapping.Keys)}");
            }

            var cacheKey = $"odds_{sportKey}";

            // Cache hit
            if (_cache.TryGetValue(cacheKey, out List<OddsApiResponse>? cachedList) && cachedList != null)
            {
                return FindMatch(cachedList, sportKey, homeTeam, awayTeam);
            }

            // Fetch from API
            var requestUrl = $"{_options.BaseUrl}/sports/{sportKey}/odds?regions={_options.Region}&markets=h2h&apiKey={_options.ApiKey}";
            _logger.LogInformation("Fetching odds from API: {SportKey}", sportKey);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(requestUrl);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed for {SportKey}", sportKey);
                return Error($"Unable to reach odds API ({ex.StatusCode ?? 0}). Check your network or API key.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning("API returned {StatusCode} for {SportKey}: {Body}",
                    (int)response.StatusCode, sportKey, body[..Math.Min(body.Length, 200)]);
                return Error($"Odds API error (HTTP {(int)response.StatusCode}). Verify your API key.");
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Empty response from API for {SportKey}", sportKey);
                return Error("No data returned from odds API. Try again later.");
            }

            List<OddsApiResponse>? list;
            try
            {
                list = JsonSerializer.Deserialize<List<OddsApiResponse>>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parse failed for {SportKey}. Raw: {Json}", sportKey, json[..Math.Min(json.Length, 200)]);
                return Error("Failed to parse odds data. API may have changed format.");
            }

            if (list == null || list.Count == 0)
            {
                _logger.LogInformation("No matches returned for {SportKey}", sportKey);
                return Error($"No upcoming matches found for {league}.");
            }

            _cache.Set(cacheKey, list, TimeSpan.FromMinutes(_options.CacheMinutes));
            _logger.LogInformation("Cached {Count} matches for {SportKey} ({Min}min TTL)",
                list.Count, sportKey, _options.CacheMinutes);

            return FindMatch(list, sportKey, homeTeam, awayTeam);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in FetchOddsAsync");
            return Error($"Unexpected error: {ex.Message}");
        }
    }

    private FetchedOdds FindMatch(List<OddsApiResponse> list, string sportKey, string homeTeam, string awayTeam)
    {
        var match = list.FirstOrDefault(m =>
            m.HomeTeam.Equals(homeTeam, StringComparison.OrdinalIgnoreCase) &&
            m.AwayTeam.Equals(awayTeam, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            _logger.LogInformation("Match {Home} vs {Away} not found in {SportKey}", homeTeam, awayTeam, sportKey);
            return Error($"Match '{homeTeam} vs {awayTeam}' not found in {sportKey}. Check team spelling.");
        }

        var result = new FetchedOdds
        {
            HomeTeam = match.HomeTeam,
            AwayTeam = match.AwayTeam,
            League = match.SportTitle,
            MatchDate = match.CommenceTime
        };

        // Bet365
        var bet365 = match.Bookmakers.FirstOrDefault(b =>
            b.Key.Equals("bet365", StringComparison.OrdinalIgnoreCase));
        if (bet365 != null)
        {
            var h2h = bet365.Markets.FirstOrDefault(m => m.Key == "h2h");
            if (h2h != null)
            {
                result.Bet365Home = h2h.Outcomes.FirstOrDefault(o => o.Name == match.HomeTeam)?.Price;
                result.Bet365Draw = h2h.Outcomes.FirstOrDefault(o => o.Name == "Draw")?.Price;
                result.Bet365Away = h2h.Outcomes.FirstOrDefault(o => o.Name == match.AwayTeam)?.Price;
            }
        }

        // Pinnacle
        var pinnacle = match.Bookmakers.FirstOrDefault(b =>
            b.Key.Equals("pinnacle", StringComparison.OrdinalIgnoreCase));
        if (pinnacle != null)
        {
            var h2h = pinnacle.Markets.FirstOrDefault(m => m.Key == "h2h");
            if (h2h != null)
            {
                result.PinnacleHome = h2h.Outcomes.FirstOrDefault(o => o.Name == match.HomeTeam)?.Price;
                result.PinnacleDraw = h2h.Outcomes.FirstOrDefault(o => o.Name == "Draw")?.Price;
                result.PinnacleAway = h2h.Outcomes.FirstOrDefault(o => o.Name == match.AwayTeam)?.Price;
            }
        }

        return result;
    }

    private static FetchedOdds Error(string message) => new() { Error = message };

    private static string? MapLeague(string league) =>
        LeagueMapping.TryGetValue(league, out var key) ? key : null;
}
