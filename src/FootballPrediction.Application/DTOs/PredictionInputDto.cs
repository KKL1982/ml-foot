namespace FootballPrediction.Application.DTOs;

public class PredictionInputDto
{
    public DateTime Date { get; set; }
    public string League { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public string? HomeCoach { get; set; }
    public string? AwayCoach { get; set; }
    public double? Bet365Home { get; set; }
    public double? Bet365Draw { get; set; }
    public double? Bet365Away { get; set; }
    public double? PinnacleHome { get; set; }
    public double? PinnacleDraw { get; set; }
    public double? PinnacleAway { get; set; }
}
