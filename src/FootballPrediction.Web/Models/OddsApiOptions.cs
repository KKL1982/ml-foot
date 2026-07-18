namespace FootballPrediction.Web.Models;

public class OddsApiOptions
{
    public const string SectionName = "OddsApi";
    public string BaseUrl { get; set; } = "https://api.the-odds-api.com/v4";
    public string ApiKey { get; set; } = string.Empty;
    public string Region { get; set; } = "eu";
    public int CacheMinutes { get; set; } = 5;
}
