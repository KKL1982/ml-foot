namespace FootballPrediction.Domain.Entities;

/// <summary>
/// Un enregistrement d'entraîneur en poste pour une équipe, avec l'historique
/// du coach. Représente l'affectation d'un entraîneur à une équipe avec une
/// date d'effet. La "seule" clé logique est (Team, Coach, EffectiveFrom).
/// </summary>
public sealed class CoachAssignment
{
    public string Team { get; set; } = string.Empty;
    public string Coach { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; } = DateTime.MinValue;
    public string? League { get; set; }

    public CoachAssignment() { }

    public CoachAssignment(string team, string coach, DateTime effectiveFrom, string? league = null)
    {
        Team = team;
        Coach = coach;
        EffectiveFrom = effectiveFrom;
        League = league;
    }
}
