namespace FootballPrediction.Domain.Entities;

public class Odds
{
    public string Bookmaker { get; set; } = string.Empty;
    public decimal HomeWin { get; set; }
    public decimal Draw { get; set; }
    public decimal AwayWin { get; set; }
}
