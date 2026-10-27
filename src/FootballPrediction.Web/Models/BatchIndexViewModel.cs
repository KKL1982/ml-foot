using FootballPrediction.Application.Services;
using FootballPrediction.Domain.Entities;

namespace FootballPrediction.Web.Models;

/// <summary>Données de la page Batch : championnats, période par défaut, CSV déjà créés.</summary>
public class BatchIndexViewModel
{
    public IReadOnlyList<Championship> Championships { get; set; } = Array.Empty<Championship>();
    public DateTime From { get; set; } = DateTime.Today;
    public DateTime To { get; set; } = DateTime.Today.AddDays(7);
    public IReadOnlyList<MatchFileInfo> Files { get; set; } = Array.Empty<MatchFileInfo>();

    /// <summary>Résultat de « Actualiser les entraîneurs » (null si non lancé).</summary>
    public CoachRefreshResult? CoachRefresh { get; set; }

    /// <summary>Message d'erreur affiché en haut de la page.</summary>
    public string? Error { get; set; }
}
