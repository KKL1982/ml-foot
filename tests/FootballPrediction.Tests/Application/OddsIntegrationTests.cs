using FluentAssertions;
using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Application.Services;
using FootballPrediction.Domain.Entities;
using FootballPrediction.Web.Services;
using Moq;
using Xunit;
using Match = FootballPrediction.Domain.Entities.Match;

namespace FootballPrediction.Tests.Application;

public class OddsIntegrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mlfoot_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static Odds O(decimal h, decimal d, decimal a) => new() { HomeWin = h, Draw = d, AwayWin = a };

    [Theory]
    [InlineData("Manchester City", "Man City")]
    [InlineData("Brighton and Hove Albion", "Brighton")]
    [InlineData("Paris Saint Germain", "Paris SG")]
    [InlineData("Atletico Madrid", "Ath Madrid")]
    [InlineData("Bayer Leverkusen", "Leverkusen")]
    [InlineData("Wolverhampton Wanderers", "Wolves")]
    public void TeamNameMapper_UnderstandsOddsApiNames(string name, string expected)
    {
        var mapped = TeamNameMapper.Map(name);
        mapped.Name.Should().Be(expected);
        mapped.Known.Should().BeTrue();
    }

    [Fact]
    public void AttachOdds_MatchesByCanonicalNamesAndDate()
    {
        var matches = new List<Match>
        {
            new() { Date = new DateTime(2026, 11, 1), HomeTeam = "Man City", AwayTeam = "Arsenal" },
            new() { Date = new DateTime(2026, 11, 1), HomeTeam = "Chelsea", AwayTeam = "Fulham" },
        };
        var odds = new List<ExternalMatchOdds>
        {
            // heure UTC tardive : le jour local peut différer de 1
            new("Manchester City", "Arsenal FC", new DateTime(2026, 11, 2, 0, 30, 0, DateTimeKind.Utc),
                new Dictionary<string, Odds> { ["Pinnacle"] = O(1.8m, 3.6m, 4.4m) }),
        };

        var attached = MatchFetchService.AttachOdds(matches, odds);

        attached.Should().Be(1);
        matches[0].BookmakerOdds["Pinnacle"].HomeWin.Should().Be(1.8m);
        matches[1].BookmakerOdds.Should().BeEmpty();
    }

    [Fact]
    public async Task Fetch_AttachesOdds_AndCsvKeepsThemOnReload()
    {
        var client = new Mock<IFootballDataClient>();
        client.Setup(c => c.GetMatchesAsync("PL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync(new List<Match>
            {
                new() { Date = new DateTime(2026, 11, 1), League = "Premier League", HomeTeam = "Arsenal FC", AwayTeam = "Chelsea FC" }
            });
        var oddsSource = new Mock<IMatchOddsSource>();
        oddsSource.Setup(o => o.GetUpcomingOddsAsync("PL", default))
            .ReturnsAsync(new List<ExternalMatchOdds>
            {
                new("Arsenal", "Chelsea", new DateTime(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc),
                    new Dictionary<string, Odds> { ["Bet365"] = O(1.95m, 3.5m, 4.2m), ["Pinnacle"] = O(2.01m, 3.55m, 4.05m) })
            });
        var store = new MatchFileStore(_dir);
        var svc = new MatchFetchService(client.Object, store, oddsSource.Object);

        var result = await svc.FetchAsync(new[] { "PL" }, new DateTime(2026, 11, 1), new DateTime(2026, 11, 7));

        result.OddsEnabled.Should().BeTrue();
        result.MatchesWithOdds.Should().Be(1);
        var reloaded = await new CsvParserService().ParseMatchesAsync(store.FindExisting(result.FileName)!);
        reloaded.Should().ContainSingle();
        reloaded[0].BookmakerOdds["Bet365"].Draw.Should().Be(3.5m);
        reloaded[0].BookmakerOdds["Pinnacle"].AwayWin.Should().Be(4.05m);
    }

    [Fact]
    public async Task Fetch_OddsFailure_IsAWarning_NotAnError()
    {
        var client = new Mock<IFootballDataClient>();
        client.Setup(c => c.GetMatchesAsync("PL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync(new List<Match> { new() { Date = DateTime.Today, League = "Premier League", HomeTeam = "Arsenal FC", AwayTeam = "Chelsea FC" } });
        var oddsSource = new Mock<IMatchOddsSource>();
        oddsSource.Setup(o => o.GetUpcomingOddsAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new FootballDataException("quota The Odds API atteint."));
        var svc = new MatchFetchService(client.Object, new MatchFileStore(_dir), oddsSource.Object);

        var result = await svc.FetchAsync(new[] { "PL" }, DateTime.Today, DateTime.Today.AddDays(3));

        result.Matches.Should().HaveCount(1);
        result.Errors.Should().BeEmpty();
        result.OddsWarnings.Should().ContainSingle().Which.Should().Contain("quota");
        result.FileName.Should().NotBeNull();
    }

    [Fact]
    public void BatchPrediction_SendsOddsToModel_WithFallbackToAverage()
    {
        IReadOnlyList<PredictionInputDto>? captured = null;
        var pred = new Mock<IPredictionService>();
        pred.Setup(p => p.PredictBatchMulticlass(It.IsAny<IReadOnlyList<PredictionInputDto>>()))
            .Callback<IReadOnlyList<PredictionInputDto>>(i => captured = i)
            .Returns(new List<PredictionOutputDto>
            {
                new() { PredictedResult = "1", Probability1 = 0.6, Probability2 = 0.4, Confidence = 0.6 },
                new() { PredictedResult = "1", Probability1 = 0.6, Probability2 = 0.4, Confidence = 0.6 },
                new() { PredictedResult = "1", Probability1 = 0.6, Probability2 = 0.4, Confidence = 0.6 },
            });
        var matches = new List<Match>
        {
            new() { Date = DateTime.Today, HomeTeam = "A", AwayTeam = "B",
                    BookmakerOdds = { ["Bet365"] = O(2m, 3m, 4m), ["Pinnacle"] = O(2.2m, 3.2m, 3.8m) } },
            new() { Date = DateTime.Today, HomeTeam = "C", AwayTeam = "D",
                    BookmakerOdds = { ["Pinnacle"] = O(1.5m, 4m, 6m) } },
            new() { Date = DateTime.Today, HomeTeam = "E", AwayTeam = "F" },
        };

        new BatchPredictionService(pred.Object).Predict(matches);

        captured.Should().NotBeNull();
        captured!.Should().HaveCount(3);
        var c = captured!;
        c[0].Bet365Home.Should().Be(2.0);
        c[0].PinnacleHome.Should().BeApproximately(2.2, 1e-9);
        c[1].Bet365Home.Should().Be(1.5);      // repli sur Pinnacle
        c[2].Bet365Home.Should().BeNull();      // aucune cote
    }
}
