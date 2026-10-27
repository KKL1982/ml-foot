namespace FootballPrediction.Web.Models;

public class BatchResultViewModel
{
    public IReadOnlyList<PredictionResultViewModel> Predictions { get; set; } = Array.Empty<PredictionResultViewModel>();
    public int TotalMatches { get; set; }
    public int SuccessfulPredictions { get; set; }
    public string? DownloadFileName { get; set; }

    /// <summary>Nom du CSV de matchs créé et utilisé pour cette prédiction (si récupéré en ligne).</summary>
    public string? SourceFileName { get; set; }

    /// <summary>Avertissements à afficher (équipes non reconnues, championnats en erreur...).</summary>
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();

    public bool HasDownload => !string.IsNullOrEmpty(DownloadFileName);
}
