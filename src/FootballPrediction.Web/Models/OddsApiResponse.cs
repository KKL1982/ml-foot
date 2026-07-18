using System.Text.Json.Serialization;

namespace FootballPrediction.Web.Models;

public class OddsApiResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("sport_key")]
    public string SportKey { get; set; } = string.Empty;

    [JsonPropertyName("sport_title")]
    public string SportTitle { get; set; } = string.Empty;

    [JsonPropertyName("commence_time")]
    public DateTime CommenceTime { get; set; }

    [JsonPropertyName("home_team")]
    public string HomeTeam { get; set; } = string.Empty;

    [JsonPropertyName("away_team")]
    public string AwayTeam { get; set; } = string.Empty;

    [JsonPropertyName("bookmakers")]
    public List<Bookmaker> Bookmakers { get; set; } = new();
}

public class Bookmaker
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("markets")]
    public List<Market> Markets { get; set; } = new();
}

public class Market
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("outcomes")]
    public List<Outcome> Outcomes { get; set; } = new();
}

public class Outcome
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public double Price { get; set; }
}

/// <summary>Résultat simplifié pour le frontend.</summary>
public class FetchedOdds
{
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public string League { get; set; } = string.Empty;
    public DateTime MatchDate { get; set; }

    public double? Bet365Home { get; set; }
    public double? Bet365Draw { get; set; }
    public double? Bet365Away { get; set; }

    public double? PinnacleHome { get; set; }
    public double? PinnacleDraw { get; set; }
    public double? PinnacleAway { get; set; }

    public string? Error { get; set; }
}
