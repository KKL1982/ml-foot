using CsvHelper.Configuration.Attributes;

namespace FootballPrediction.Application.Services;

internal class MatchCsvRow
{
    [Name("Date")]
    public string? Date { get; set; }

    [Name("League")]
    public string? League { get; set; }

    [Name("Season")]
    public string? Season { get; set; }

    [Name("HomeTeam")]
    public string? HomeTeam { get; set; }

    [Name("AwayTeam")]
    public string? AwayTeam { get; set; }

    [Name("HomeGoals")]
    public string? HomeGoals { get; set; }

    [Name("AwayGoals")]
    public string? AwayGoals { get; set; }

    [Name("Result")]
    public string? Result { get; set; }

    [Name("HomeCoach")]
    public string? HomeCoach { get; set; }

    [Name("AwayCoach")]
    public string? AwayCoach { get; set; }

    [Name("Bet365_1")]
    public string? Bet365_1 { get; set; }

    [Name("Bet365_X")]
    public string? Bet365_X { get; set; }

    [Name("Bet365_2")]
    public string? Bet365_2 { get; set; }

    [Name("Pinnacle_1")]
    public string? Pinnacle_1 { get; set; }

    [Name("Pinnacle_X")]
    public string? Pinnacle_X { get; set; }

    [Name("Pinnacle_2")]
    public string? Pinnacle_2 { get; set; }

    [Name("WilliamHill_1")]
    public string? WilliamHill_1 { get; set; }

    [Name("WilliamHill_X")]
    public string? WilliamHill_X { get; set; }

    [Name("WilliamHill_2")]
    public string? WilliamHill_2 { get; set; }
}
