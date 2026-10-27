using FluentAssertions;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Application.Services;
using Moq;
using Xunit;
using Match = FootballPrediction.Domain.Entities.Match;

namespace FootballPrediction.Tests.Application;

public class TeamNameMapperTests
{
    [Theory]
    [InlineData("Manchester City FC", "Man City")]
    [InlineData("Paris Saint-Germain FC", "Paris SG")]
    [InlineData("FC Bayern München", "Bayern Munich")]
    [InlineData("Club Atlético de Madrid", "Ath Madrid")]
    [InlineData("Borussia Mönchengladbach", "M'gladbach")]
    [InlineData("Nottingham Forest FC", "Nott'm Forest")]
    public void Map_KnownApiName_ReturnsCsvName(string api, string expected)
    {
        var mapped = TeamNameMapper.Map(api);
        mapped.Name.Should().Be(expected);
        mapped.Known.Should().BeTrue();
    }

    [Fact]
    public void Map_AlreadyCanonical_IsKnown()
    {
        var mapped = TeamNameMapper.Map("Man City");
        mapped.Name.Should().Be("Man City");
        mapped.Known.Should().BeTrue();
    }

    [Fact]
    public void Map_Unknown_IsFlaggedAndCleaned()
    {
        var mapped = TeamNameMapper.Map("Nouvelle Equipe FC");
        mapped.Name.Should().Be("Nouvelle Equipe");
        mapped.Known.Should().BeFalse();
    }
}

public class MatchCsvWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mlfoot_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public async Task WrittenFile_IsReadBackByParser_WithSameMatches()
    {
        var matches = new List<Match>
        {
            new() { Date = new DateTime(2026, 11, 1), League = "Premier League", Season = "2026-2027",
                    HomeTeam = "Man City", AwayTeam = "Nott'm Forest", HomeCoach = "Pep, Guardiola" },
            new() { Date = new DateTime(2026, 11, 2), League = "LaLiga", Season = "2026-2027",
                    HomeTeam = "Ath Madrid", AwayTeam = "Betis" },
        };
        var path = Path.Combine(_dir, "t.csv");

        await MatchCsvWriter.WriteAsync(path, matches);
        var parsed = await new CsvParserService().ParseMatchesAsync(path);

        parsed.Should().HaveCount(2);
        parsed[0].Date.Should().Be(new DateTime(2026, 11, 1));
        parsed[0].HomeTeam.Should().Be("Man City");
        parsed[0].AwayTeam.Should().Be("Nott'm Forest");
        parsed[0].HomeCoach.Should().Be("Pep, Guardiola");
        parsed[1].League.Should().Be("LaLiga");
        parsed.Should().OnlyContain(m => m.Result == null);
    }
}

public class MatchFileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mlfoot_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Theory]
    [InlineData("../secret.csv")]
    [InlineData("..\\secret.csv")]
    [InlineData("a/b.csv")]
    [InlineData("notcsv.txt")]
    [InlineData("")]
    [InlineData(null)]
    public void FindExisting_InvalidName_ReturnsNull(string? name)
    {
        new MatchFileStore(_dir).FindExisting(name).Should().BeNull();
    }

    [Fact]
    public async Task SaveThenList_ReturnsFileWithMatchCount()
    {
        var store = new MatchFileStore(_dir);
        await store.SaveAsync("PL_1.csv", new[]
        {
            new Match { Date = DateTime.Today, League = "Premier League", HomeTeam = "A", AwayTeam = "B" },
            new Match { Date = DateTime.Today, League = "Premier League", HomeTeam = "C", AwayTeam = "D" },
        });

        var files = store.List();
        files.Should().ContainSingle();
        files[0].FileName.Should().Be("PL_1.csv");
        files[0].MatchCount.Should().Be(2);
        store.FindExisting("PL_1.csv").Should().NotBeNull();
    }
}

public class MatchFetchServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mlfoot_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public async Task Fetch_NormalizesNames_SavesCsv_FlagsUnknownTeams()
    {
        var client = new Mock<IFootballDataClient>();
        client.Setup(c => c.GetMatchesAsync("PL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync(new List<Match>
            {
                new() { Date = new DateTime(2026, 11, 2), League = "Premier League", HomeTeam = "Manchester City FC", AwayTeam = "Inconnu FC" },
                new() { Date = new DateTime(2026, 11, 1), League = "Premier League", HomeTeam = "Arsenal FC", AwayTeam = "Chelsea FC" },
            });
        var svc = new MatchFetchService(client.Object, new MatchFileStore(_dir));

        var result = await svc.FetchAsync(new[] { "PL" }, new DateTime(2026, 11, 1), new DateTime(2026, 11, 7));

        result.Matches.Select(m => m.HomeTeam).Should().Equal("Arsenal", "Man City");
        result.UnknownTeams.Should().Equal("Inconnu");
        result.FileName.Should().Be("matches_PL_20261101_20261107.csv");
        File.Exists(Path.Combine(_dir, result.FileName!)).Should().BeTrue();
    }

    [Fact]
    public async Task Fetch_OneLeagueFails_KeepsOthersAndReportsError()
    {
        var client = new Mock<IFootballDataClient>();
        client.Setup(c => c.GetMatchesAsync("PL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ThrowsAsync(new FootballDataException("quota"));
        client.Setup(c => c.GetMatchesAsync("LL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync(new List<Match> { new() { Date = DateTime.Today, League = "LaLiga", HomeTeam = "Girona FC", AwayTeam = "Sevilla FC" } });
        var svc = new MatchFetchService(client.Object, new MatchFileStore(_dir));

        var result = await svc.FetchAsync(new[] { "PL", "LL" }, DateTime.Today, DateTime.Today.AddDays(3));

        result.Matches.Should().HaveCount(1);
        result.Errors.Should().ContainSingle().Which.Should().Contain("quota");
    }

    [Fact]
    public async Task Fetch_NoMatches_CreatesNoFile()
    {
        var client = new Mock<IFootballDataClient>();
        client.Setup(c => c.GetMatchesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync(new List<Match>());
        var svc = new MatchFetchService(client.Object, new MatchFileStore(_dir));

        var result = await svc.FetchAsync(new[] { "PL" }, DateTime.Today, DateTime.Today);

        result.FileName.Should().BeNull();
        Directory.Exists(_dir).Should().BeFalse();
    }
}

public class CoachRefreshServiceTests
{
    private static (CoachRefreshService svc, InMemoryCoachRepository repo) Create(params ExternalTeamCoach[] coaches)
    {
        var client = new Mock<IFootballDataClient>();
        client.Setup(c => c.GetTeamCoachesAsync(It.IsAny<string>(), default))
            .ReturnsAsync((string code, CancellationToken _) =>
                code == "PL" ? coaches.ToList() : new List<ExternalTeamCoach>());
        var repo = new InMemoryCoachRepository();
        return (new CoachRefreshService(client.Object, repo, new CoachService(repo)), repo);
    }

    [Fact]
    public async Task Refresh_NewCoach_IsRecordedAndReported()
    {
        var (svc, repo) = Create(new ExternalTeamCoach("Arsenal FC", "Mikel Arteta"));

        var result = await svc.RefreshAsync(new[] { "PL" }, new DateTime(2026, 11, 1));

        result.Changed.Should().ContainSingle();
        result.Changed[0].Team.Should().Be("Arsenal");
        result.Changed[0].PreviousCoach.Should().BeNull();
        repo.GetCoach("Arsenal", new DateTime(2026, 11, 2)).Should().Be("Mikel Arteta");
    }

    [Fact]
    public async Task Refresh_SameCoachTwice_SecondRunChangesNothing()
    {
        var (svc, repo) = Create(new ExternalTeamCoach("Arsenal FC", "Mikel Arteta"));
        await svc.RefreshAsync(new[] { "PL" }, new DateTime(2026, 11, 1));

        var second = await svc.RefreshAsync(new[] { "PL" }, new DateTime(2026, 11, 8));

        second.Changed.Should().BeEmpty();
        second.Unchanged.Should().Be(1);
        repo.GetHistory("Arsenal").Should().HaveCount(1);
    }

    [Fact]
    public async Task Refresh_CoachChange_KeepsHistory()
    {
        var repo = new InMemoryCoachRepository();
        var coachService = new CoachService(repo);
        coachService.UpdateCoach("Arsenal", "Old Coach", new DateTime(2026, 1, 1));
        var client = new Mock<IFootballDataClient>();
        client.Setup(c => c.GetTeamCoachesAsync("PL", default))
            .ReturnsAsync(new List<ExternalTeamCoach> { new("Arsenal FC", "New Coach") });
        var svc = new CoachRefreshService(client.Object, repo, coachService);

        var result = await svc.RefreshAsync(new[] { "PL" }, new DateTime(2026, 11, 1));

        result.Changed.Should().ContainSingle().Which.PreviousCoach.Should().Be("Old Coach");
        repo.GetCoach("Arsenal", new DateTime(2026, 6, 1)).Should().Be("Old Coach");
        repo.GetCoach("Arsenal", new DateTime(2026, 12, 1)).Should().Be("New Coach");
    }

    [Fact]
    public async Task Refresh_ApiError_IsReportedNotThrown()
    {
        var client = new Mock<IFootballDataClient>();
        client.Setup(c => c.GetTeamCoachesAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new FootballDataException("clé absente"));
        var repo = new InMemoryCoachRepository();
        var svc = new CoachRefreshService(client.Object, repo, new CoachService(repo));

        var result = await svc.RefreshAsync(new[] { "PL" }, DateTime.Today);

        result.Errors.Should().ContainSingle().Which.Should().Contain("clé absente");
        result.Changed.Should().BeEmpty();
    }
}
