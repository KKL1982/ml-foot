using FluentAssertions;
using FootballPrediction.Application.Services;
using Xunit;

namespace FootballPrediction.Tests.Application;

public class ChampionshipServiceTests
{
    // Crée un CSV temporaire avec les 4 championnats + quelques matchs + équipes.
    private static string CreateTempCsv()
    {
        var csv = @"Date,League,Season,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Result,HomeCoach,AwayCoach
2024-08-16,Premier League,2024-2025,Arsenal,Wolves,2,0,1,Arteta,ONeil
2024-08-17,Premier League,2024-2025,Chelsea,Man City,0,2,2,Maresca,Guardiola
2024-08-18,LaLiga,2024-2025,Real Madrid,Barcelona,3,1,1,Ancelotti,Flick
2024-08-18,Ligue 1,2024-2025,PSG,Lyon,2,1,1,LuisEnrique,Sage
2024-08-19,Bundesliga,2024-2025,Bayern,Dortmund,4,0,1,Kompany,Sahin";
        var path = Path.GetTempFileName();
        File.WriteAllText(path, csv);
        return path;
    }

    private static ChampionshipService CreateService()
    {
        return new ChampionshipService(new CsvParserService(), CreateTempCsv());
    }

    [Fact]
    public void GetChampionships_ShouldReturnFourDefaults()
    {
        var svc = CreateService();
        var champs = svc.GetChampionships();
        champs.Should().HaveCount(4);
        champs.Select(c => c.Code).Should().Contain(new[] { "PL", "LL", "L1", "BL" });
    }

    [Fact]
    public async Task LoadMatchesAsync_ByCode_ReturnsOnlyThatChampionship()
    {
        var svc = CreateService();
        var matches = await svc.LoadMatchesAsync("PL");
        matches.Should().HaveCount(2);
        matches.All(m => m.League == "Premier League").Should().BeTrue();
    }

    [Fact]
    public async Task LoadMatchesAsync_ByName_ReturnsOnlyThatChampionship()
    {
        var svc = CreateService();
        var matches = await svc.LoadMatchesAsync("LaLiga");
        matches.Should().HaveCount(1);
        matches.Single().HomeTeam.Should().Be("Real Madrid");
    }

    [Fact]
    public async Task LoadMatchesAsync_UnknownChampionship_ReturnsEmpty()
    {
        var svc = CreateService();
        var matches = await svc.LoadMatchesAsync("SerieA");
        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadMatchesAsync_DateRange_FiltersAndSorts()
    {
        var svc = CreateService();
        var from = new DateTime(2024, 8, 17);
        var to = new DateTime(2024, 8, 18);
        var matches = await svc.LoadMatchesAsync("PL", from, to);
        // Le match du 16/08 sort de la plage, seul celui du 17/08 reste.
        matches.Should().HaveCount(1);
        matches.Single().AwayTeam.Should().Be("Man City");
    }

    [Fact]
    public void GetTeams_ReturnsDistinctSortedTeams()
    {
        var svc = CreateService();
        var teams = svc.GetTeams("PL");
        // Arsenal, Chelsea, Man City, Wolves
        teams.Should().Contain(new[] { "Arsenal", "Chelsea", "Man City", "Wolves" });
        teams.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveTeam_CaseInsensitive_ReturnsCanonicalName()
    {
        var svc = CreateService();
        svc.ResolveTeam("PL", "arsenal").Should().Be("Arsenal");
        svc.ResolveTeam("PL", "NOPE").Should().BeNull();
    }
}
