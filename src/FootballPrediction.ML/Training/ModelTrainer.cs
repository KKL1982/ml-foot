using FootballPrediction.Domain.Enums;
using FootballPrediction.ML.DataModels;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace FootballPrediction.ML.Training;

public class ModelTrainer
{
    private readonly MLContext _mlContext;

    public ModelTrainer()
    {
        _mlContext = new MLContext(seed: 42);
    }

    public (ITransformer model, MulticlassClassificationMetrics? metrics)
        TrainAndEvaluate(IReadOnlyList<MatchData> data, float testFraction = 0.2f)
    {
        var dataView = _mlContext.Data.LoadFromEnumerable(data);

        var pipeline = _mlContext.Transforms
            .Conversion.MapValueToKey(nameof(MatchData.Label), nameof(MatchData.Label))
            .Append(_mlContext.Transforms.Concatenate("Features",
                nameof(MatchData.Bet365HomeProb),
                nameof(MatchData.Bet365DrawProb),
                nameof(MatchData.Bet365AwayProb),
                nameof(MatchData.HomeForm5),
                nameof(MatchData.AwayForm5),
                nameof(MatchData.HomeGoalsForAvg),
                nameof(MatchData.AwayGoalsForAvg),
                nameof(MatchData.HomeGoalsAgainstAvg),
                nameof(MatchData.AwayGoalsAgainstAvg),
                nameof(MatchData.FormDiff)
            ))
            .Append(_mlContext.MulticlassClassification.Trainers
                .SdcaMaximumEntropy(nameof(MatchPrediction.PredictedResult), "Features"))
            .Append(_mlContext.Transforms.Conversion.MapKeyToValue(nameof(MatchPrediction.PredictedResult)));

        if (data.Count < 10)
        {
            var model = pipeline.Fit(dataView);
            return (model, null);
        }

        var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: testFraction);

        var trainedModel = pipeline.Fit(split.TrainSet);
        var predictions = trainedModel.Transform(split.TestSet);
        var metrics = _mlContext.MulticlassClassification.Evaluate(predictions,
            labelColumnName: nameof(MatchData.Label),
            predictedLabelColumnName: nameof(MatchPrediction.PredictedResult));

        return (trainedModel, metrics);
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
