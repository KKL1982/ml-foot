namespace FootballPrediction.Application.DTOs;

/// <summary>
/// Résultat de prédiction enrichi pour l'affichage en batch : réunit la
/// prédiction 1X2 (probabilités + confiance), les coachs à jour des deux
/// équipes et la décision de mise (HOME/AWAY/SKIP). Utilisé par les vues
/// tableau (CLI console + Web) et par l'API.
/// </summary>
public sealed class BatchPredictionItem
{
    public DateTime Date { get; set; }
    public string League { get; set; } = string.Empty;
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public string? HomeCoach { get; set; }
    public string? AwayCoach { get; set; }

    public string PredictedResult { get; set; } = string.Empty;
    public double Probability1 { get; set; }
    public double ProbabilityX { get; set; }
    public double Probability2 { get; set; }
    public double Confidence { get; set; }
    public string Bet { get; set; } = "SKIP";
    public string Comment { get; set; } = string.Empty;
}
