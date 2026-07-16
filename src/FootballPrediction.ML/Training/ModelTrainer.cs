using FootballPrediction.Domain.Enums;
using FootballPrediction.ML.DataModels;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace FootballPrediction.ML.Training;

public class ModelTrainer
{
    private readonly MLContext _mlContext;

    public ModelTrainer()
    {
        _mlContext = new MLContext(seed: 42);
    }

    public enum TrainerKind
    {
        SdcaMaximumEntropy,
        LbfgsMaximumEntropy,
        LightGbm
    }

    public record TrainingResult(
        ITransformer Model,
        MulticlassClassificationMetrics? Metrics,
        TrainerKind Trainer,
        double Accuracy,
        double LogLoss);

    public TrainingResult TrainAndEvaluate(
        IReadOnlyList<MatchData> data,
        TrainerKind trainer = TrainerKind.SdcaMaximumEntropy,
        double trainFraction = 0.8)
    {
        int trainCount = (int)(data.Count * trainFraction);
        var trainData = data.Take(trainCount).ToList();
        var testData = data.Skip(trainCount).ToList();

        var pipeline = BuildPipeline(trainer);

        var trainView = _mlContext.Data.LoadFromEnumerable(trainData);
        var model = pipeline.Fit(trainView);

        MulticlassClassificationMetrics? metrics = null;
        if (testData.Count >= 10)
        {
            var testView = _mlContext.Data.LoadFromEnumerable(testData);
            var predictions = model.Transform(testView);
            metrics = _mlContext.MulticlassClassification.Evaluate(
                predictions,
                labelColumnName: "Label",
                predictedLabelColumnName: nameof(MatchPrediction.PredictedResult));
        }

        return new TrainingResult(
            model,
            metrics,
            trainer,
            metrics?.MicroAccuracy ?? 0,
            metrics?.LogLoss ?? 0);
    }

    public List<TrainingResult> TrainAllAndCompare(IReadOnlyList<MatchData> data, double trainFraction = 0.8)
    {
        var results = new List<TrainingResult>();

        foreach (var trainer in new[] { TrainerKind.SdcaMaximumEntropy, TrainerKind.LbfgsMaximumEntropy, TrainerKind.LightGbm })
        {
            try
            {
                var result = TrainAndEvaluate(data, trainer, trainFraction);
                results.Add(result);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Trainer {trainer} failed: {ex.Message}");
            }
        }

        return results;
    }

    private IEstimator<ITransformer> BuildPipeline(TrainerKind trainer)
    {
        // Use IEstimator<ITransformer> to avoid generic type mismatch when chaining
        // different transformer types (OneHotEncoding → Concatenate → MapValueToKey → Trainer → MapKeyToValue)
        IEstimator<ITransformer> pipeline = _mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.League));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.Season)));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.HomeTeam)));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.AwayTeam)));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.HomeCoach)));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.AwayCoach)));

        pipeline = pipeline.Append(_mlContext.Transforms.Concatenate("Features",
            nameof(MatchData.League),
            nameof(MatchData.Season),
            nameof(MatchData.HomeTeam),
            nameof(MatchData.AwayTeam),
            nameof(MatchData.HomeCoach),
            nameof(MatchData.AwayCoach),
            nameof(MatchData.Bet365HomeProb),
            nameof(MatchData.Bet365DrawProb),
            nameof(MatchData.Bet365AwayProb),
            nameof(MatchData.PinnacleHomeProb),
            nameof(MatchData.PinnacleDrawProb),
            nameof(MatchData.PinnacleAwayProb),
            nameof(MatchData.HomeForm5),
            nameof(MatchData.AwayForm5),
            nameof(MatchData.HomeGoalsForAvg),
            nameof(MatchData.AwayGoalsForAvg),
            nameof(MatchData.HomeGoalsAgainstAvg),
            nameof(MatchData.AwayGoalsAgainstAvg),
            nameof(MatchData.FormDiff),
            nameof(MatchData.HomeCoachTenure),
            nameof(MatchData.AwayCoachTenure)
        ));

        pipeline = pipeline.Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", "Label"));

        pipeline = trainer switch
        {
            TrainerKind.SdcaMaximumEntropy => pipeline
                .Append(_mlContext.MulticlassClassification.Trainers
                    .SdcaMaximumEntropy("Label", "Features")),

            TrainerKind.LbfgsMaximumEntropy => pipeline
                .Append(_mlContext.MulticlassClassification.Trainers
                    .LbfgsMaximumEntropy("Label", "Features")),

            TrainerKind.LightGbm => pipeline
                .Append(_mlContext.MulticlassClassification.Trainers
                    .LightGbm("Label", "Features")),

            _ => throw new ArgumentException($"Unknown trainer: {trainer}")
        };

        pipeline = pipeline
            .Append(_mlContext.Transforms.Conversion.MapKeyToValue(nameof(MatchPrediction.PredictedResult)));

        return pipeline;
    }

    public void SaveModel(ITransformer model, IDataView schema, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var stream = File.Create(outputPath);
        _mlContext.Model.Save(model, schema.Schema, stream);
    }

    public IDataView GetDataView(IReadOnlyList<MatchData> data)
    {
        return _mlContext.Data.LoadFromEnumerable(data);
    }
}
