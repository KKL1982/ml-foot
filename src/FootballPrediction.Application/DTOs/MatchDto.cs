namespace FootballPrediction.Application.DTOs;

public class MatchDto
{
    public DateTime Date { get; set; }
    public string League { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
    public string? Result { get; set; }
    public string? HomeCoach { get; set; }
    public string? AwayCoach { get; set; }
    public Dictionary<string, OddsDto> BookmakerOdds { get; set; } = new();
}

public class OddsDto
{
    public string Bookmaker { get; set; } = string.Empty;
    public decimal HomeWin { get; set; }
    public decimal Draw { get; set; }
    public decimal AwayWin { get; set; }
}
