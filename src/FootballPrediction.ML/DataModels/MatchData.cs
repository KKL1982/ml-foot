using FootballPrediction.Domain.Entities;

namespace FootballPrediction.ML.DataModels;

public class MatchData
{
    // Encoded categoricals (OneHot — stored as float for pipeline compatibility)
    public string League { get; set; } = string.Empty;
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string HomeCoach { get; set; } = string.Empty;
    public string AwayCoach { get; set; } = string.Empty;

    // Bet365 probabilities
    public float Bet365HomeProb { get; set; }
    public float Bet365DrawProb { get; set; }
    public float Bet365AwayProb { get; set; }

    // Pinnacle probabilities
    public float PinnacleHomeProb { get; set; }
    public float PinnacleDrawProb { get; set; }
    public float PinnacleAwayProb { get; set; }

    // Form features (last 5 matches)
    public float HomeForm5 { get; set; }
    public float AwayForm5 { get; set; }
    public float HomeGoalsForAvg { get; set; }
    public float AwayGoalsForAvg { get; set; }
    public float HomeGoalsAgainstAvg { get; set; }
    public float AwayGoalsAgainstAvg { get; set; }
    public float FormDiff { get; set; }

    // Coach stability
    public float HomeCoachTenure { get; set; }
    public float AwayCoachTenure { get; set; }

    // Label
    public string Label { get; set; } = string.Empty;
}
