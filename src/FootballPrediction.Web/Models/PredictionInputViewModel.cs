using System.ComponentModel.DataAnnotations;

namespace FootballPrediction.Web.Models;

public class PredictionInputViewModel
{
    [Required(ErrorMessage = "Date is required.")]
    [DataType(DataType.Date)]
    public DateTime? Date { get; set; }

    [Required(ErrorMessage = "League is required.")]
    public string League { get; set; } = string.Empty;

    [Required(ErrorMessage = "Home team is required.")]
    public string HomeTeam { get; set; } = string.Empty;

    [Required(ErrorMessage = "Away team is required.")]
    public string AwayTeam { get; set; } = string.Empty;

    public string? HomeCoach { get; set; }
    public string? AwayCoach { get; set; }

    [Range(1.01, 999, ErrorMessage = "Bet365 home odds must be > 1.00")]
    public double? Bet365Home { get; set; }

    [Range(1.01, 999, ErrorMessage = "Bet365 draw odds must be > 1.00")]
    public double? Bet365Draw { get; set; }

    [Range(1.01, 999, ErrorMessage = "Bet365 away odds must be > 1.00")]
    public double? Bet365Away { get; set; }
}
