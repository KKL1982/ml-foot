using FootballPrediction.Domain.Enums;
using FootballPrediction.ML.DataModels;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace FootballPrediction.ML.Training;

public class ModelTrainer
{
    private readonly MLContext _mlContext;
    private NeuralNetworkTrainer? _nnTrainer;

    public ModelTrainer()
    {
        _mlContext = new MLContext(seed: 42);
    }

    public enum TrainerKind
    {
        SdcaMaximumEntropy,
        LbfgsMaximumEntropy,
        LightGbm,
        NeuralNetwork,
        SdcaLogisticRegression,
        LbfgsLogisticRegression,
        LightGbmBinary
    }

    public record TrainingResult(
        ITransformer? Model,
        MulticlassClassificationMetrics? Metrics,
        TrainerKind Trainer,
        double Accuracy,
        double LogLoss,
        string? HyperParams = null,
        NeuralNetwork? NeuralNet = null,
        CalibratedBinaryClassificationMetrics? BinaryMetrics = null,
        double? Precision = null,
        double? Recall = null,
        double? F1Score = null)
    {
        public string FormattedAccuracy => Accuracy.ToString("P2");
    }

    public record LightGbmOptions(
        int NumberOfLeaves = 20,
        double LearningRate = 0.1,
        int MinimumExampleCountPerLeaf = 10)
    {
        public override string ToString() =>
            $"Leaves={NumberOfLeaves}, LR={LearningRate}, MinData={MinimumExampleCountPerLeaf}";
    }

    /// <summary>Returns true if this trainer is a binary classifier.</summary>
    public static bool IsBinary(TrainerKind kind) => kind switch
    {
        TrainerKind.SdcaLogisticRegression or TrainerKind.LbfgsLogisticRegression or TrainerKind.LightGbmBinary => true,
        _ => false
    };

    // ──────────────── MULTICLASS (1/X/2) ────────────────

    public TrainingResult TrainAndEvaluate(
        IReadOnlyList<MatchData> data,
        TrainerKind trainer = TrainerKind.SdcaMaximumEntropy,
        double trainFraction = 0.8,
        LightGbmOptions? lightGbmOptions = null,
        bool binaryMode = false)
    {
        int trainCount = (int)(data.Count * trainFraction);
        var trainData = data.Take(trainCount).ToList();
        var testData = data.Skip(trainCount).ToList();

        // Neural network takes a different path
        if (trainer == TrainerKind.NeuralNetwork)
            return TrainNeuralNetwork(trainData, testData, binaryMode);

        // Binary ML.NET trainers
        if (IsBinary(trainer))
            return TrainBinaryML(data, trainer, trainFraction, lightGbmOptions);

        var pipeline = BuildPipeline(trainer, lightGbmOptions);
        var trainView = _mlContext.Data.LoadFromEnumerable(trainData);
        var model = pipeline.Fit(trainView);

        MulticlassClassificationMetrics? metrics = null;
        if (testData.Count >= 10)
        {
            var testView = _mlContext.Data.LoadFromEnumerable(testData);
            var predictions = model.Transform(testView);
            metrics = _mlContext.MulticlassClassification.Evaluate(
                predictions, labelColumnName: "Label", predictedLabelColumnName: nameof(MatchPrediction.PredictedResult));
        }

        return new TrainingResult(model, metrics, trainer,
            metrics?.MicroAccuracy ?? 0, metrics?.LogLoss ?? 0,
            lightGbmOptions?.ToString());
    }

    private TrainingResult TrainBinaryML(
        IReadOnlyList<MatchData> data, TrainerKind trainer, double trainFraction, LightGbmOptions? opts)
    {
        int trainCount = (int)(data.Count * trainFraction);
        var trainData = data.Take(trainCount).ToList();
        var testData = data.Skip(trainCount).ToList();

        var pipeline = BuildBinaryPipeline(trainer, opts);
        var trainView = _mlContext.Data.LoadFromEnumerable(trainData);
        var model = pipeline.Fit(trainView);

        CalibratedBinaryClassificationMetrics? metrics = null;
        if (testData.Count >= 10)
        {
            var testView = _mlContext.Data.LoadFromEnumerable(testData);
            var predictions = model.Transform(testView);
            metrics = _mlContext.BinaryClassification.Evaluate(
                predictions, labelColumnName: nameof(MatchData.HomeWin), scoreColumnName: "Score");
        }

        return new TrainingResult(model, null, trainer,
            metrics?.Accuracy ?? 0, metrics?.LogLoss ?? 0,
            opts?.ToString(),
            BinaryMetrics: metrics,
            Precision: metrics?.PositivePrecision,
            Recall: metrics?.PositiveRecall,
            F1Score: metrics?.F1Score);
    }

    private TrainingResult TrainNeuralNetwork(List<MatchData> trainData, List<MatchData> testData, bool binaryMode)
    {
        _nnTrainer = new NeuralNetworkTrainer();
        var epochs = binaryMode ? 50 : 30;
        var result = _nnTrainer.Train(trainData, testData, epochs: epochs, binaryMode: binaryMode);
        return new TrainingResult(null, result.Metrics, TrainerKind.NeuralNetwork,
            result.Accuracy, result.LogLoss, $"epochs={result.Epochs}, lr={result.LearningRate:F4}");
    }

    // ──────────────── COMPARISON ────────────────

    public List<TrainingResult> TrainAllAndCompare(
        IReadOnlyList<MatchData> data, double trainFraction = 0.8, bool binaryMode = false)
    {
        var results = new List<TrainingResult>();
        var trainers = binaryMode
            ? new[] { TrainerKind.SdcaLogisticRegression, TrainerKind.LbfgsLogisticRegression, TrainerKind.LightGbmBinary, TrainerKind.NeuralNetwork }
            : new[] { TrainerKind.SdcaMaximumEntropy, TrainerKind.LbfgsMaximumEntropy, TrainerKind.LightGbm, TrainerKind.NeuralNetwork };

        foreach (var trainer in trainers)
        {
            try
            {
                Console.WriteLine($"Training {trainer}...");
                var result = TrainAndEvaluate(data, trainer, trainFraction, binaryMode: binaryMode);
                results.Add(result);
                if (binaryMode)
                    Console.WriteLine($"  {trainer}: Accuracy={result.Accuracy:P2}, LogLoss={result.LogLoss:F4}, F1={result.F1Score:F4}");
                else
                    Console.WriteLine($"  {trainer}: Accuracy={result.Accuracy:P2}, LogLoss={result.LogLoss:F4}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Trainer {trainer} failed: {ex.Message}");
            }
        }

        return results;
    }

    // ──────────────── TUNING ────────────────

    public List<TrainingResult> TuneLightGbm(
        IReadOnlyList<MatchData> data, double trainFraction = 0.8,
        int[]? numLeavesOptions = null, double[]? learningRateOptions = null, int[]? minDataOptions = null)
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

    // ──────────────── PIPELINES ────────────────

    private IEstimator<ITransformer> BuildPipeline(TrainerKind trainer, LightGbmOptions? lightGbmOptions = null)
    {
        IEstimator<ITransformer> pipeline = _mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.League));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.Season)));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.HomeTeam)));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.AwayTeam)));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.HomeCoach)));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.AwayCoach)));

        pipeline = pipeline.Append(_mlContext.Transforms.Concatenate("Features",
            nameof(MatchData.League), nameof(MatchData.Season),
            nameof(MatchData.HomeTeam), nameof(MatchData.AwayTeam),
            nameof(MatchData.HomeCoach), nameof(MatchData.AwayCoach),
            nameof(MatchData.Bet365HomeProb), nameof(MatchData.Bet365DrawProb), nameof(MatchData.Bet365AwayProb),
            nameof(MatchData.PinnacleHomeProb), nameof(MatchData.PinnacleDrawProb), nameof(MatchData.PinnacleAwayProb),
            nameof(MatchData.HomeForm5), nameof(MatchData.AwayForm5),
            nameof(MatchData.HomeGoalsForAvg), nameof(MatchData.AwayGoalsForAvg),
            nameof(MatchData.HomeGoalsAgainstAvg), nameof(MatchData.AwayGoalsAgainstAvg),
            nameof(MatchData.FormDiff),
            nameof(MatchData.HomeCoachTenure), nameof(MatchData.AwayCoachTenure)
        ));

        pipeline = pipeline.Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", "Label"));

        pipeline = trainer switch
        {
            TrainerKind.SdcaMaximumEntropy => pipeline.Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features")),
            TrainerKind.LbfgsMaximumEntropy => pipeline.Append(_mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy("Label", "Features")),
            TrainerKind.LightGbm => pipeline.Append(_mlContext.MulticlassClassification.Trainers.LightGbm("Label", "Features",
                numberOfLeaves: lightGbmOptions?.NumberOfLeaves, learningRate: lightGbmOptions?.LearningRate,
                minimumExampleCountPerLeaf: lightGbmOptions?.MinimumExampleCountPerLeaf)),
            _ => throw new ArgumentException($"Unknown trainer: {trainer}")
        };

        pipeline = pipeline.Append(_mlContext.Transforms.Conversion.MapKeyToValue(nameof(MatchPrediction.PredictedResult), "PredictedLabel"));
        return pipeline;
    }

    private IEstimator<ITransformer> BuildBinaryPipeline(TrainerKind trainer, LightGbmOptions? opts = null)
    {
        // Use HomeWin bool field directly — no CustomMapping needed
        IEstimator<ITransformer> pipeline = _mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.League));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.Season)));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.HomeTeam)));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.AwayTeam)));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.HomeCoach)));
        pipeline = pipeline.Append(_mlContext.Transforms.Categorical.OneHotEncoding(nameof(MatchData.AwayCoach)));

        pipeline = pipeline.Append(_mlContext.Transforms.Concatenate("Features",
            nameof(MatchData.League), nameof(MatchData.Season),
            nameof(MatchData.HomeTeam), nameof(MatchData.AwayTeam),
            nameof(MatchData.HomeCoach), nameof(MatchData.AwayCoach),
            nameof(MatchData.Bet365HomeProb), nameof(MatchData.Bet365DrawProb), nameof(MatchData.Bet365AwayProb),
            nameof(MatchData.PinnacleHomeProb), nameof(MatchData.PinnacleDrawProb), nameof(MatchData.PinnacleAwayProb),
            nameof(MatchData.HomeForm5), nameof(MatchData.AwayForm5),
            nameof(MatchData.HomeGoalsForAvg), nameof(MatchData.AwayGoalsForAvg),
            nameof(MatchData.HomeGoalsAgainstAvg), nameof(MatchData.AwayGoalsAgainstAvg),
            nameof(MatchData.FormDiff),
            nameof(MatchData.HomeCoachTenure), nameof(MatchData.AwayCoachTenure)
        ));

        pipeline = trainer switch
        {
            TrainerKind.SdcaLogisticRegression => pipeline.Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(nameof(MatchData.HomeWin), "Features")),
            TrainerKind.LbfgsLogisticRegression => pipeline.Append(_mlContext.BinaryClassification.Trainers.LbfgsLogisticRegression(nameof(MatchData.HomeWin), "Features")),
            TrainerKind.LightGbmBinary => pipeline.Append(_mlContext.BinaryClassification.Trainers.LightGbm(nameof(MatchData.HomeWin), "Features",
                numberOfLeaves: opts?.NumberOfLeaves, learningRate: opts?.LearningRate,
                minimumExampleCountPerLeaf: opts?.MinimumExampleCountPerLeaf)),
            _ => throw new ArgumentException($"Unknown binary trainer: {trainer}")
        };

        pipeline = pipeline.Append(_mlContext.Transforms.CopyColumns(nameof(MatchPrediction.HomeWinProbability), "Probability"));

        return pipeline;
    }

    // ──────────────── SAVE / LOAD ────────────────

    public void SaveModel(ITransformer model, IDataView schema, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        using var stream = File.Create(outputPath);
        _mlContext.Model.Save(model, schema.Schema, stream);
    }

    public void SaveNeuralNetwork(NeuralNetwork nn, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);
        nn.Save(writer);
    }

    public NeuralNetwork? LoadNeuralNetwork(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Model file not found: {modelPath}");
        using var stream = File.OpenRead(modelPath);
        using var reader = new BinaryReader(stream);
        return NeuralNetwork.Load(reader);
    }

    public IDataView GetDataView(IReadOnlyList<MatchData> data) => _mlContext.Data.LoadFromEnumerable(data);
}

// ──────────────── Helper types for CustomMapping ────────────────

internal class BinaryLabel { public bool Label { get; set; } }

internal class BinaryPrediction
{
    public bool PredictedLabel { get; set; }
    public float Probability { get; set; }
}
