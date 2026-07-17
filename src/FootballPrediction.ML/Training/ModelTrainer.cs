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
        double LogLoss,
        string? HyperParams = null);

    public record LightGbmOptions(
        int NumberOfLeaves = 20,
        double LearningRate = 0.1,
        int MinimumExampleCountPerLeaf = 10)
    {
        public override string ToString() =>
            $"Leaves={NumberOfLeaves}, LR={LearningRate}, MinData={MinimumExampleCountPerLeaf}";
    }

    public TrainingResult TrainAndEvaluate(
        IReadOnlyList<MatchData> data,
        TrainerKind trainer = TrainerKind.SdcaMaximumEntropy,
        double trainFraction = 0.8,
        LightGbmOptions? lightGbmOptions = null)
    {
        int trainCount = (int)(data.Count * trainFraction);
        var trainData = data.Take(trainCount).ToList();
        var testData = data.Skip(trainCount).ToList();

        var pipeline = BuildPipeline(trainer, lightGbmOptions);

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
            metrics?.LogLoss ?? 0,
            lightGbmOptions?.ToString());
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

    /// <summary>
    /// Grid search over LightGbm hyperparameters. Returns results sorted by accuracy descending.
    /// </summary>
    public List<TrainingResult> TuneLightGbm(
        IReadOnlyList<MatchData> data,
        double trainFraction = 0.8,
        int[]? numLeavesOptions = null,
        double[]? learningRateOptions = null,
        int[]? minDataOptions = null)
    {
        numLeavesOptions ??= new[] { 10, 20, 31, 50 };
        learningRateOptions ??= new[] { 0.05, 0.1, 0.2 };
        minDataOptions ??= new[] { 5, 10, 20 };

        var results = new List<TrainingResult>();
        int total = numLeavesOptions.Length * learningRateOptions.Length * minDataOptions.Length;
        int done = 0;

        foreach (var leaves in numLeavesOptions)
        foreach (var lr in learningRateOptions)
        foreach (var minData in minDataOptions)
        {
            done++;
            var opts = new LightGbmOptions(leaves, lr, minData);
            try
            {
                var result = TrainAndEvaluate(data, TrainerKind.LightGbm, trainFraction, opts);
                results.Add(result);
                Console.WriteLine($"[{done}/{total}] {opts} => Accuracy={result.Accuracy:P2} LogLoss={result.LogLoss:F4}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{done}/{total}] {opts} => FAILED: {ex.Message}");
            }
        }

        results.Sort((a, b) => b.Accuracy.CompareTo(a.Accuracy));
        return results;
    }

    private IEstimator<ITransformer> BuildPipeline(TrainerKind trainer, LightGbmOptions? lightGbmOptions = null)
    {
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
                .Append(_mlContext.MulticlassClassification.Trainers.LightGbm(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    numberOfLeaves: lightGbmOptions?.NumberOfLeaves,
                    learningRate: lightGbmOptions?.LearningRate,
                    minimumExampleCountPerLeaf: lightGbmOptions?.MinimumExampleCountPerLeaf)),

            _ => throw new ArgumentException($"Unknown trainer: {trainer}")
        };

        pipeline = pipeline
            .Append(_mlContext.Transforms.Conversion.MapKeyToValue(nameof(MatchPrediction.PredictedResult), "PredictedLabel"));

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
