using System.ComponentModel.DataAnnotations;

namespace FootballPrediction.Web.Models;

public class PredictionInputViewModel
{
    [Display(Name = "Date")]
    public DateTime? Date { get; set; } = DateTime.Today;

    [Required]
    [Display(Name = "Championnat")]
    public string League { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Équipe domicile")]
    public string HomeTeam { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Équipe extérieure")]
    public string AwayTeam { get; set; } = string.Empty;

    [Display(Name = "Coach domicile")]
    public string? HomeCoach { get; set; }

    [Display(Name = "Coach extérieur")]
    public string? AwayCoach { get; set; }

    // Bet365
    [Display(Name = "Cote Bet365 — 1 (domicile)")]
    public double? Bet365Home { get; set; }

    [Display(Name = "Cote Bet365 — X (nul)")]
    public double? Bet365Draw { get; set; }

    [Display(Name = "Cote Bet365 — 2 (extérieur)")]
    public double? Bet365Away { get; set; }

    // Pinnacle
    [Display(Name = "Cote Pinnacle — 1 (domicile)")]
    public double? PinnacleHome { get; set; }

    [Display(Name = "Cote Pinnacle — X (nul)")]
    public double? PinnacleDraw { get; set; }

    [Display(Name = "Cote Pinnacle — 2 (extérieur)")]
    public double? PinnacleAway { get; set; }

    // William Hill
    [Display(Name = "Cote William Hill — 1 (domicile)")]
    public double? WilliamHillHome { get; set; }

    [Display(Name = "Cote William Hill — X (nul)")]
    public double? WilliamHillDraw { get; set; }

    [Display(Name = "Cote William Hill — 2 (extérieur)")]
    public double? WilliamHillAway { get; set; }

    // Model
    public bool ModelLoaded { get; set; }
    public string? ModelPath { get; set; }
    public string Mode { get; set; } = "Binary";

    // Binary threshold
    [Display(Name = "Seuil de confiance")]
    [Range(0.5, 1.0)]
    public double BinaryThreshold { get; set; } = 0.5;
}
