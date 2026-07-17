using FluentAssertions;
using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Web.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FootballPrediction.Tests.Application;

public class PredictionServiceTests
{
    private static IPredictionService CreateService()
    {
        // MatchPredictor needs a model to work, so we mock the service
        var predictor = new FootballPrediction.ML.Prediction.MatchPredictor();
        // Try loading binary model
        var modelPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "models", "model_binary.zip");
        try { if (File.Exists(modelPath)) predictor.LoadModel(modelPath); } catch { }
        var logger = new Mock<ILogger<PredictionService>>().Object;
        return new PredictionService(predictor, logger);
    }

    [Fact]
    public void PredictBinary_WithOdds_ShouldReturnHomeOrAway()
    {
        var service = CreateService();
        var input = new PredictionInputDto
        {
            Date = DateTime.Today,
            League = "Premier League",
            HomeTeam = "Arsenal",
            AwayTeam = "Chelsea",
            Bet365Home = 1.80,
            Bet365Draw = 3.75,
            Bet365Away = 4.50
        };

        var result = service.PredictBinary(input, threshold: 0.5);

        result.HomeTeam.Should().Be("Arsenal");
        result.AwayTeam.Should().Be("Chelsea");
        result.HomeWinProbability.Should().BeInRange(0, 1);
        result.AwayWinProbability.Should().BeInRange(0, 1);
        (result.HomeWinProbability + result.AwayWinProbability).Should().BeApproximately(1.0, 0.01);
        result.Confidence.Should().BeInRange(0, 1);
        result.Bet.Should().BeOneOf("HOME", "AWAY", "SKIP");
        result.Comment.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PredictBinary_WithHighThreshold_ShouldSkipUncertainMatches()
    {
        var service = CreateService();
        var input = new PredictionInputDto
        {
            Date = DateTime.Today,
            League = "LaLiga",
            HomeTeam = "Barcelona",
            AwayTeam = "Real Madrid"
        };

        var result = service.PredictBinary(input, threshold: 0.95);

        // With no odds and high threshold, should skip
        result.Bet.Should().Be("SKIP");
    }

    [Fact]
    public void PredictMulticlass_ShouldReturnThreeProbabilities()
    {
        var service = CreateService();
        var input = new PredictionInputDto
        {
            Date = DateTime.Today,
            League = "Bundesliga",
            HomeTeam = "Bayern",
            AwayTeam = "Dortmund",
            Bet365Home = 1.50,
            Bet365Draw = 4.50,
            Bet365Away = 6.00
        };

        var result = service.PredictMulticlass(input);

        result.PredictedResult.Should().BeOneOf("1", "X", "2");
        (result.Probability1 + result.ProbabilityX + result.Probability2).Should().BeInRange(0.95, 1.05);
        result.Comment.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PredictBatchBinary_ShouldReturnSameCount()
    {
        var service = CreateService();
        var inputs = new List<PredictionInputDto>
        {
            new() { Date = DateTime.Today, League = "PL", HomeTeam = "A", AwayTeam = "B", Bet365Home = 2.0, Bet365Draw = 3.5, Bet365Away = 4.0 },
            new() { Date = DateTime.Today, League = "PL", HomeTeam = "C", AwayTeam = "D", Bet365Home = 1.5, Bet365Draw = 4.0, Bet365Away = 7.0 }
        };

        var results = service.PredictBatchBinary(inputs);

        results.Should().HaveCount(2);
        results.All(r => r.Bet == "HOME" || r.Bet == "AWAY" || r.Bet == "SKIP").Should().BeTrue();
    }

    [Fact]
    public void PredictBatchMulticlass_ShouldReturnSameCount()
    {
        var service = CreateService();
        var inputs = new List<PredictionInputDto>
        {
            new() { Date = DateTime.Today, League = "PL", HomeTeam = "X", AwayTeam = "Y", Bet365Home = 2.0, Bet365Draw = 3.5, Bet365Away = 4.0 },
            new() { Date = DateTime.Today, League = "PL", HomeTeam = "Z", AwayTeam = "W", Bet365Home = 3.0, Bet365Draw = 3.0, Bet365Away = 2.5 }
        };

        var results = service.PredictBatchMulticlass(inputs);

        results.Should().HaveCount(2);
        results.All(r => r.PredictedResult == "1" || r.PredictedResult == "X" || r.PredictedResult == "2").Should().BeTrue();
    }
}
