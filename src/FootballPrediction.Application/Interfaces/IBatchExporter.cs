using FootballPrediction.Application.DTOs;

namespace FootballPrediction.Application.Interfaces;

/// <summary>Exporte une liste de prédictions batch vers un format de fichier binaire (xlsx, pdf...).</summary>
public interface IBatchExporter
{
    string ContentType { get; }
    string Extension { get; }
    byte[] Export(IReadOnlyList<BatchPredictionItem> items, DateTime generatedAt);
}
