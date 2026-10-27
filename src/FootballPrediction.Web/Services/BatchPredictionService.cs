using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Domain.Entities;

namespace FootballPrediction.Web.Services;

/// <summary>
/// Orchestrate la prédiction en batch de matchs NON encore joués.
/// Construit les features (en appliquant les coachs à jour via un
/// ICoachRepository si fourni), prédit en multiclass (1/X/2) et produit une
/// liste enrichie de <see cref="BatchPredictionItem"/> prête pour l'affichage
/// tableau (CLI console ou Web) et pour l'API.
/// </summary>
public class BatchPredictionService
{
    private readonly IPredictionService _predictionService;
    private readonly ICoachRepository? _coachRepository;

    public BatchPredictionService(IPredictionService predictionService, ICoachRepository? coachRepository = null)
    {
        _predictionService = predictionService;
        _coachRepository = coachRepository;
    }

    /// <summary>
    /// Prédit les résultats d'une liste de matchs (ex. matchs à venir chargés
    /// depuis un championnat). Applique les coachs à jour et retourne un item
    /// de prédiction par match d'entrée.
    /// </summary>
    public IReadOnlyList<BatchPredictionItem> Predict(IReadOnlyList<Match> matches, double threshold = 0.5)
    {
        if (matches.Count == 0)
            return Array.Empty<BatchPredictionItem>();

        // Construit les entrées de prédiction directement depuis les matchs
        // (à venir, donc non joués) en appliquant les coachs à jour. On prédit
        // via IPredictionService sans passer par BuildFeatures (qui ne traite
        // que les matchs JOUÉS avec résultat — inadapté aux matchs à venir).
        var inputs = matches.Select(m => new PredictionInputDto
        {
            Date = m.Date,
            League = m.League,
            Season = m.Season,
            HomeTeam = m.HomeTeam,
            AwayTeam = m.AwayTeam,
            HomeCoach = _coachRepository?.GetCoach(m.HomeTeam, m.Date) ?? m.HomeCoach,
            AwayCoach = _coachRepository?.GetCoach(m.AwayTeam, m.Date) ?? m.AwayCoach,
        }).ToList();

        IReadOnlyList<PredictionOutputDto> preds;
        try
        {
            preds = _predictionService.PredictBatchMulticlass(inputs);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Batch] Prédiction impossible : {ex.Message}");
            preds = Array.Empty<PredictionOutputDto>();
        }

        var items = new List<BatchPredictionItem>(matches.Count);
        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var homeCoach = _coachRepository?.GetCoach(match.HomeTeam, match.Date) ?? match.HomeCoach;
            var awayCoach = _coachRepository?.GetCoach(match.AwayTeam, match.Date) ?? match.AwayCoach;

            BatchPredictionItem item;
            if (i < preds.Count)
            {
                var p = preds[i];
                item = new BatchPredictionItem
                {
                    Date = match.Date,
                    League = match.League,
                    HomeTeam = match.HomeTeam,
                    AwayTeam = match.AwayTeam,
                    HomeCoach = homeCoach,
                    AwayCoach = awayCoach,
                    PredictedResult = p.PredictedResult,
                    Probability1 = p.Probability1,
                    ProbabilityX = p.ProbabilityX,
                    Probability2 = p.Probability2,
                    Confidence = p.Confidence,
                    Comment = p.Comment,
                };
            }
            else
            {
                item = new BatchPredictionItem
                {
                    Date = match.Date,
                    League = match.League,
                    HomeTeam = match.HomeTeam,
                    AwayTeam = match.AwayTeam,
                    HomeCoach = homeCoach,
                    AwayCoach = awayCoach,
                    PredictedResult = "?",
                    Confidence = 0,
                    Comment = "Aucune prédiction (pas assez de contexte)",
                };
            }

            item.Bet = ComputeBet(item, threshold);
            items.Add(item);
        }

        return items;
    }

    private static string ComputeBet(BatchPredictionItem item, double threshold)
    {
        var maxProb = Math.Max(item.Probability1, Math.Max(item.ProbabilityX, item.Probability2));
        if (maxProb < threshold)
            return "SKIP";
        if (maxProb == item.Probability1 && item.Probability1 > item.Probability2)
            return "HOME";
        if (maxProb == item.Probability2 && item.Probability2 > item.Probability1)
            return "AWAY";
        return "SKIP";
    }
}
