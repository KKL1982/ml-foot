using FluentAssertions;
using FootballPrediction.Domain.Entities;
using FootballPrediction.Domain.Enums;
using FootballPrediction.ML.FeatureEngineering;
using Xunit;

namespace FootballPrediction.ML.Tests.FeatureEngineering;

public class FormCalculatorTests
{
    private static List<Match> CreateSampleMatches() => new()
    {
        new Match { Date = new DateTime(2024, 1, 1), HomeTeam = "TeamA", AwayTeam = "TeamB", HomeGoals = 2, AwayGoals = 1 },
        new Match { Date = new DateTime(2024, 1, 8), HomeTeam = "TeamA", AwayTeam = "TeamC", HomeGoals = 3, AwayGoals = 0 },
        new Match { Date = new DateTime(2024, 1, 15), HomeTeam = "TeamD", AwayTeam = "TeamA", HomeGoals = 1, AwayGoals = 1 },
        new Match { Date = new DateTime(2024, 1, 22), HomeTeam = "TeamA", AwayTeam = "TeamE", HomeGoals = 1, AwayGoals = 2 },
        new Match { Date = new DateTime(2024, 1, 29), HomeTeam = "TeamF", AwayTeam = "TeamA", HomeGoals = 0, AwayGoals = 3 },
        new Match { Date = new DateTime(2024, 2, 5), HomeTeam = "TeamA", AwayTeam = "TeamG", HomeGoals = 2, AwayGoals = 2 },
    };

    [Fact]
    public void CalculateForm_WithRecent5_ShouldCountCorrectly()
    {
        var matches = CreateSampleMatches();
        var form = FormCalculator.CalculateForm("TeamA", new DateTime(2024, 2, 10), matches, windowSize: 5);

        form.MatchesPlayed.Should().Be(5);
        form.Wins.Should().Be(2);
        form.Draws.Should().Be(2);
        form.Losses.Should().Be(1);
    }

    [Fact]
    public void CalculateForm_AllWins_ShouldReturnMaxPoints()
    {
        var matches = new List<Match>
        {
            new() { Date = new DateTime(2024, 1, 1), HomeTeam = "TeamA", AwayTeam = "TeamB", HomeGoals = 1, AwayGoals = 0 },
            new() { Date = new DateTime(2024, 1, 8), HomeTeam = "TeamA", AwayTeam = "TeamC", HomeGoals = 2, AwayGoals = 1 },
            new() { Date = new DateTime(2024, 1, 15), HomeTeam = "TeamD", AwayTeam = "TeamA", HomeGoals = 0, AwayGoals = 1 },
        };

        var form = FormCalculator.CalculateForm("TeamA", new DateTime(2024, 2, 1), matches, windowSize: 5);

        form.Wins.Should().Be(3);
        form.Losses.Should().Be(0);
        form.PointsPerGame.Should().Be(3.0);
    }

    [Fact]
    public void CalculateForm_EmptyHistory_ShouldReturnZeroes()
    {
        var form = FormCalculator.CalculateForm("UnknownTeam", new DateTime(2024, 2, 1), new List<Match>(), windowSize: 5);

        form.MatchesPlayed.Should().Be(0);
        form.PointsPerGame.Should().Be(0);
    }

    [Fact]
    public void CalculateForm_ShouldNotIncludeFutureMatches()
    {
        var matches = new List<Match>
        {
            new() { Date = new DateTime(2024, 1, 1), HomeTeam = "TeamA", AwayTeam = "TeamB", HomeGoals = 1, AwayGoals = 0 },
            new() { Date = new DateTime(2024, 2, 1), HomeTeam = "TeamA", AwayTeam = "TeamC", HomeGoals = 1, AwayGoals = 0 },
        };

        var form = FormCalculator.CalculateForm("TeamA", new DateTime(2024, 1, 15), matches, windowSize: 5);

        form.MatchesPlayed.Should().Be(1);
    }

    [Fact]
    public void FormDiff_ShouldBePositiveWhenHomeFormIsBetter()
    {
        var homeForm = new FormCalculator.TeamForm(5, 3, 1, 1, 2.0, 1.0, 1.0, 2.0);
        var awayForm = new FormCalculator.TeamForm(5, 1, 2, 2, 0.5, 1.5, -1.0, 1.0);

        var diff = FormCalculator.FormDiff(homeForm, awayForm);

        diff.Should().BeApproximately(1.0, 0.01);
    }
}
