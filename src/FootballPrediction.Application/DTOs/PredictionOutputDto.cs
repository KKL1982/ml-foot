namespace FootballPrediction.Application.DTOs;

public class PredictionOutputDto
{
    public DateTime Date { get; set; }
    public string League { get; set; } = string.Empty;
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public string PredictedResult { get; set; } = string.Empty;
    public double Probability1 { get; set; }
    public double ProbabilityX { get; set; }
    public double Probability2 { get; set; }
    public double Confidence { get; set; }
    public string Comment { get; set; } = string.Empty;
}
