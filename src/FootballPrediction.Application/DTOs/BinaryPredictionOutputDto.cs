namespace FootballPrediction.Application.DTOs;

public class BinaryPredictionOutputDto
{
    public DateTime Date { get; set; }
    public string League { get; set; } = string.Empty;
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public double HomeWinProbability { get; set; }
    public double AwayWinProbability { get; set; }
    public double Confidence { get; set; }
    public string Bet { get; set; } = "SKIP"; // HOME, AWAY, SKIP
    public string Comment { get; set; } = string.Empty;
}
