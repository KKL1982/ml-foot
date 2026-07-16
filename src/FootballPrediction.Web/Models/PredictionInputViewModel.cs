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

    [Display(Name = "Cote Bet365 — 1 (domicile)")]
    public double? Bet365Home { get; set; }

    [Display(Name = "Cote Bet365 — X (nul)")]
    public double? Bet365Draw { get; set; }

    [Display(Name = "Cote Bet365 — 2 (extérieur)")]
    public double? Bet365Away { get; set; }

    [Display(Name = "Cote Pinnacle — 1 (domicile)")]
    public double? PinnacleHome { get; set; }

    [Display(Name = "Cote Pinnacle — X (nul)")]
    public double? PinnacleDraw { get; set; }

    [Display(Name = "Cote Pinnacle — 2 (extérieur)")]
    public double? PinnacleAway { get; set; }

    public bool ModelLoaded { get; set; }
    public string? ModelPath { get; set; }
}
