using FootballPrediction.ML.DataModels;
using Microsoft.ML;

namespace FootballPrediction.ML.Prediction;

public class MatchPredictor
{
    private readonly MLContext _mlContext;
    private ITransformer? _model;

    public MatchPredictor()
    {
        _mlContext = new MLContext(seed: 42);
    }

    public void LoadModel(string modelPath)
    {
        _model = _mlContext.Model.Load(modelPath, out _);
    }

    public IReadOnlyList<MatchPrediction> Predict(IReadOnlyList<MatchData> matches)
    {
        if (_model == null)
            throw new InvalidOperationException("Model not loaded. Call LoadModel first.");

        var dataView = _mlContext.Data.LoadFromEnumerable(matches);
        var predictions = _model.Transform(dataView);

        return _mlContext.Data
            .CreateEnumerable<MatchPrediction>(predictions, reuseRowObject: false)
            .ToList();
    }
}
