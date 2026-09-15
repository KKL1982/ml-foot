using FluentAssertions;
using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Domain.Entities;
using FootballPrediction.Web.Services;
using Moq;
using Xunit;
using Match = FootballPrediction.Domain.Entities.Match;

namespace FootballPrediction.Tests.Web;

public class BatchPredictionServiceTests
{
    private static Mock<IPredictionService> PredictorReturning(PredictionOutputDto dto, int count)
    {
        var mock = new Mock<IPredictionService>();
        mock.Setup(s => s.PredictBatchMulticlass(It.IsAny<IReadOnlyList<PredictionInputDto>>()))
            .Returns(Enumerable.Range(0, count).Select(_ => dto).ToList());
        return mock;
    }

    private static Match MakeMatch(string home, string away, DateTime date, string? hc = null, string? ac = null)
        => new()
        {
            Date = date,
            League = "Premier League",
            Season = "2024-2025",
            HomeTeam = home,
            AwayTeam = away,
            HomeCoach = hc,
            AwayCoach = ac,
        };

    [Fact]
    public void Predict_EmptyMatches_ReturnsEmpty()
    {
        var svc = new BatchPredictionService(new Mock<IPredictionService>().Object);
        svc.Predict(Array.Empty<Match>()).Should().BeEmpty();
    }

    [Fact]
    public void Predict_PopulatesItems_SameCountAsInput()
    {
        var pred = new PredictionOutputDto
        {
            PredictedResult = "1",
            Probability1 = 0.6, ProbabilityX = 0.25, Probability2 = 0.15,
            Confidence = 0.6, Comment = "Home win likely"
        };
        var mock = PredictorReturning(pred, 2);
        var matches = new List<Match>
        {
            MakeMatch("Arsenal", "Chelsea", new DateTime(2026, 9, 20), "Arteta", null),
            MakeMatch("Man City", "Liverpool", new DateTime(2026, 9, 21), "Guardiola", null),
        };

        var svc = new BatchPredictionService(mock.Object);
        var items = svc.Predict(matches);

        items.Should().HaveCount(2);
        items[0].HomeTeam.Should().Be("Arsenal");
        items[0].PredictedResult.Should().Be("1");
        items[0].Confidence.Should().Be(0.6);
    }

    [Fact]
    public void Predict_AppliesUpdatedCoaches_FromRepository()
    {
        var pred = new PredictionOutputDto { PredictedResult = "1", Confidence = 0.5 };
        var mock = PredictorReturning(pred, 1);

        var repo = new TestCoachRepo(); // coachs à jour via ICoachRepository
        repo.Set("Arsenal", "NewCoach");

        var matches = new List<Match> { MakeMatch("Arsenal", "Chelsea", new DateTime(2026, 9, 20), "OldCoach", null) };

        var svc = new BatchPredictionService(mock.Object, repo);
        var items = svc.Predict(matches);

        items.Single().HomeCoach.Should().Be("NewCoach");
    }

    [Fact]
    public void Predict_NoPrediction_ResultIsUnknownAndBetSkip()
    {
        var mock = new Mock<IPredictionService>();
        mock.Setup(s => s.PredictBatchMulticlass(It.IsAny<IReadOnlyList<PredictionInputDto>>()))
            .Returns(new List<PredictionOutputDto>());

        var matches = new List<Match> { MakeMatch("Arsenal", "Chelsea", new DateTime(2026, 9, 20)) };

        var svc = new BatchPredictionService(mock.Object);
        var items = svc.Predict(matches);

        items.Should().HaveCount(1);
        items[0].PredictedResult.Should().Be("?");
        items[0].Bet.Should().Be("SKIP");
    }

    private sealed class TestCoachRepo : ICoachRepository
    {
        private readonly Dictionary<string, string> _coaches = new(StringComparer.OrdinalIgnoreCase);
        public void Set(string team, string coach) => _coaches[team] = coach;

        public string? GetCoach(string team, DateTime asOfDate) =>
            _coaches.TryGetValue(team, out var c) ? c : null;
        public IReadOnlyList<CoachAssignment> GetHistory(string team) => Array.Empty<CoachAssignment>();
        public void Upsert(CoachAssignment assignment) { }
        public IReadOnlyList<string> GetTeams() => _coaches.Keys.ToList();
    }
}
