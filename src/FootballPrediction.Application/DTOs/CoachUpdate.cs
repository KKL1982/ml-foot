namespace FootballPrediction.Application.DTOs;

/// <summary>
/// Demande de mise à jour d'un entraîneur pour une équipe.
/// </summary>
public sealed class CoachUpdate
{
    public string Team { get; set; } = string.Empty;
    public string Coach { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; } = DateTime.MinValue;
    public string? League { get; set; }
}
