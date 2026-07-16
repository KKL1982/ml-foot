namespace FootballPrediction.ML.DataModels;

public class MatchPrediction
{
    public string PredictedResult { get; set; } = string.Empty;
    public float Probability1 { get; set; }
    public float ProbabilityX { get; set; }
    public float Probability2 { get; set; }
}
