using FootballPrediction.Domain.Enums;

namespace FootballPrediction.Domain.Entities;

public class Match
{
    public DateTime Date { get; set; }
    public string League { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
    public MatchResult? Result { get; set; }
    public string? HomeCoach { get; set; }
    public string? AwayCoach { get; set; }
    public Dictionary<string, Odds> BookmakerOdds { get; set; } = new();
}
