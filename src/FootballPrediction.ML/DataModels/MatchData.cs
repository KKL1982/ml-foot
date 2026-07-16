using FootballPrediction.Domain.Entities;

namespace FootballPrediction.ML.DataModels;

public class MatchData
{
    public float League { get; set; }
    public float HomeTeam { get; set; }
    public float AwayTeam { get; set; }
    public float Bet365HomeProb { get; set; }
    public float Bet365DrawProb { get; set; }
    public float Bet365AwayProb { get; set; }
    public float HomeForm5 { get; set; }
    public float AwayForm5 { get; set; }
    public float HomeGoalsForAvg { get; set; }
    public float AwayGoalsForAvg { get; set; }
    public float HomeGoalsAgainstAvg { get; set; }
    public float AwayGoalsAgainstAvg { get; set; }
    public float FormDiff { get; set; }
    public string Label { get; set; } = string.Empty;
}
