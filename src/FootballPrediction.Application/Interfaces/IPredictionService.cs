using FootballPrediction.Application.DTOs;

namespace FootballPrediction.Application.Interfaces;

public interface IPredictionService
{
    /// <summary>Binary prediction: Home/Away only, returns HomeWinProbability + Bet (HOME/AWAY/SKIP).</summary>
    BinaryPredictionOutputDto PredictBinary(PredictionInputDto input, double threshold = 0.5);

    /// <summary>Multiclass prediction: 1/X/2 with three probabilities.</summary>
    PredictionOutputDto PredictMulticlass(PredictionInputDto input);

    /// <summary>Batch binary prediction from parsed matches.</summary>
    IReadOnlyList<BinaryPredictionOutputDto> PredictBatchBinary(IReadOnlyList<PredictionInputDto> inputs, double threshold = 0.5);

    /// <summary>Batch multiclass prediction from parsed matches.</summary>
    IReadOnlyList<PredictionOutputDto> PredictBatchMulticlass(IReadOnlyList<PredictionInputDto> inputs);
}
