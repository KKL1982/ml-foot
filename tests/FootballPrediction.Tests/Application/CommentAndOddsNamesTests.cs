using FluentAssertions;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Application.Services;
using FootballPrediction.Domain.Entities;
using FootballPrediction.Web.Services;
using Moq;
using Xunit;
using Match = FootballPrediction.Domain.Entities.Match;

namespace FootballPrediction.Tests.Application;

public class CommentAndOddsNamesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mlfoot_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Theory]
    [InlineData("1", 0.92, "Strong home favorite")]
    [InlineData("2", 0.92, "Strong away favorite")]
    [InlineData("2", 0.73, "Strong away favorite")]
    [InlineData("1", 0.65, "Home win likely")]
    [InlineData("2", 0.57, "Away win likely")]
    [InlineData("2", 0.52, "Balanced match")]
    [InlineData("1", 0.40, "Balanced match")]
    [InlineData("X", 0.60, "Draw likely")]
    public void DescribeOutcome_UsesTheWinningProbability(string result, double conf, string expected)
        => PredictionService.DescribeOutcome(result, conf).Should().Be(expected);

    [Fact]
    public async Task Fetch_ListsMatchesWithoutOddsByName()
    {
        var client = new Mock<IFootballDataClient>();
        client.Setup(c => c.GetMatchesAsync("PL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync(new List<Match>
            {
                new() { Date = new DateTime(2026, 11, 1), League = "Premier League", HomeTeam = "Arsenal FC", AwayTeam = "Chelsea FC" },
                new() { Date = new DateTime(2026, 11, 2), League = "Premier League", HomeTeam = "Fulham FC", AwayTeam = "Everton FC" },
            });
        var oddsSource = new Mock<IMatchOddsSource>();
        oddsSource.Setup(o => o.GetUpcomingOddsAsync("PL", default))
            .ReturnsAsync(new List<ExternalMatchOdds>
            {
                new("Arsenal", "Chelsea", new DateTime(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc),
                    new Dictionary<string, Odds> { ["Pinnacle"] = new() { HomeWin = 2m, Draw = 3m, AwayWin = 4m } })
            });
        var svc = new MatchFetchService(client.Object, new MatchFileStore(_dir), oddsSource.Object);

        var result = await svc.FetchAsync(new[] { "PL" }, new DateTime(2026, 11, 1), new DateTime(2026, 11, 7));

        result.MatchesWithOdds.Should().Be(1);
        result.MatchesWithoutOdds.Should().ContainSingle().Which.Should().Be("Fulham – Everton (02/11)");
    }
}
