namespace FootballPrediction.Web.Models;

public class BatchResultViewModel
{
    public IReadOnlyList<PredictionResultViewModel> Predictions { get; set; } = Array.Empty<PredictionResultViewModel>();
    public int TotalMatches { get; set; }
    public int SuccessfulPredictions { get; set; }
    public string? DownloadFileName { get; set; }
}
