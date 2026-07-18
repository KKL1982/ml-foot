using System.Net.Http.Json;
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

    public async Task<FetchedOdds?> FetchOddsAsync(string league, string homeTeam, string awayTeam)
    {
        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogWarning("Odds API key not configured");
            return new FetchedOdds { Error = "API key not configured. Set OddsApi:ApiKey in appsettings.json." };
        }

        var sportKey = MapLeague(league);
        if (sportKey == null)
        {
            _logger.LogWarning("Unknown league '{League}'", league);
            return new FetchedOdds { Error = $"League '{league}' not supported for live odds." };
        }

        var cacheKey = $"odds_{sportKey}";
        List<OddsApiResponse>? cachedList = null;

        // Try cache first
        if (_cache.TryGetValue(cacheKey, out List<OddsApiResponse>? cached))
            cachedList = cached;

        if (cachedList == null)
        {
            var requestUrl = $"{_options.BaseUrl}/sports/{sportKey}/odds?regions={_options.Region}&markets=h2h&apiKey={_options.ApiKey}";
            _logger.LogInformation("Fetching odds from {Url}", requestUrl);

            try
            {
                var response = await _httpClient.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                cachedList = JsonSerializer.Deserialize<List<OddsApiResponse>>(json, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch odds for {SportKey}", sportKey);
                return new FetchedOdds { Error = $"Failed to fetch odds: {ex.Message}" };
            }

            if (cachedList != null)
            {
                _cache.Set(cacheKey, cachedList, TimeSpan.FromMinutes(_options.CacheMinutes));
                _logger.LogInformation("Cached {Count} matches for {SportKey} ({Minutes}min TTL)",
                    cachedList.Count, sportKey, _options.CacheMinutes);
            }
        }

        // Find the specific match
        var match = cachedList?.FirstOrDefault(m =>
            m.HomeTeam.Equals(homeTeam, StringComparison.OrdinalIgnoreCase) &&
            m.AwayTeam.Equals(awayTeam, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            _logger.LogInformation("Match {Home} vs {Away} not found in {SportKey} odds", homeTeam, awayTeam, sportKey);
            return new FetchedOdds { Error = $"Match '{homeTeam} vs {awayTeam}' not found. Check team names or try manual entry." };
        }

        var result = new FetchedOdds
        {
            HomeTeam = match.HomeTeam,
            AwayTeam = match.AwayTeam,
            League = match.SportTitle,
            MatchDate = match.CommenceTime
        };

        // Extract Bet365 odds
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

        // Extract Pinnacle odds
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

        _logger.LogInformation("Fetched odds: {Home} vs {Away} — Bet365: {B1}/{BX}/{B2}, Pinnacle: {P1}/{PX}/{P2}",
            result.HomeTeam, result.AwayTeam,
            result.Bet365Home, result.Bet365Draw, result.Bet365Away,
            result.PinnacleHome, result.PinnacleDraw, result.PinnacleAway);

        return result;
    }

    private static string? MapLeague(string league) =>
        LeagueMapping.TryGetValue(league, out var key) ? key : null;
}
