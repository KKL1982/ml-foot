namespace FootballPrediction.Web.Models;

public class FootballDataOptions
{
    public const string SectionName = "FootballData";
    public string BaseUrl { get; set; } = "https://api.football-data.org/v4";
    public string ApiKey { get; set; } = string.Empty;
    public int CacheMinutes { get; set; } = 10;
    /// <summary>Dossier des CSV de matchs créés (relatif au dossier de l'application).</summary>
    public string MatchesDirectory { get; set; } = "data/matches";
}
