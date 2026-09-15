using FluentAssertions;
using FootballPrediction.Application.Services;
using FootballPrediction.Domain.Entities;
using Xunit;

namespace FootballPrediction.Tests.Application;

public class InMemoryCoachRepositoryTests
{
    private static DateTime D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void GetCoach_NoAssignment_ReturnsNull()
    {
        var repo = new InMemoryCoachRepository();
        repo.GetCoach("Arsenal", D(2024, 1, 1)).Should().BeNull();
    }

    [Fact]
    public void GetCoach_ExactDate_ReturnsActiveCoach()
    {
        var repo = new InMemoryCoachRepository();
        repo.Upsert(new CoachAssignment("Arsenal", "Arteta", D(2019, 12, 1)));
        repo.GetCoach("Arsenal", D(2024, 5, 1)).Should().Be("Arteta");
    }

    [Fact]
    public void GetCoach_BeforeEffectiveDate_ReturnsNull()
    {
        var repo = new InMemoryCoachRepository();
        repo.Upsert(new CoachAssignment("Arsenal", "Arteta", D(2019, 12, 1)));
        repo.GetCoach("Arsenal", D(2019, 6, 1)).Should().BeNull();
    }

    [Fact]
    public void GetCoach_MultipleCoaches_ReturnsMostRecent()
    {
        var repo = new InMemoryCoachRepository();
        repo.Upsert(new CoachAssignment("Arsenal", "Arteta", D(2019, 12, 1)));
        repo.Upsert(new CoachAssignment("Arsenal", "NewCoach", D(2025, 7, 1)));

        repo.GetCoach("Arsenal", D(2024, 5, 1)).Should().Be("Arteta");
        repo.GetCoach("Arsenal", D(2025, 8, 1)).Should().Be("NewCoach");
    }

    [Fact]
    public void Upsert_SameTeamCoachDate_IsIdempotent()
    {
        var repo = new InMemoryCoachRepository();
        repo.Upsert(new CoachAssignment("Arsenal", "Arteta", D(2019, 12, 1)));
        repo.Upsert(new CoachAssignment("Arsenal", "Arteta", D(2019, 12, 1)));
        repo.GetHistory("Arsenal").Should().HaveCount(1);
    }

    [Fact]
    public void GetHistory_ReturnsChronologicalOrder()
    {
        var repo = new InMemoryCoachRepository();
        repo.Upsert(new CoachAssignment("Arsenal", "New", D(2025, 7, 1)));
        repo.Upsert(new CoachAssignment("Arsenal", "Arteta", D(2019, 12, 1)));

        var hist = repo.GetHistory("Arsenal");
        hist[0].Coach.Should().Be("Arteta");
        hist[1].Coach.Should().Be("New");
    }

    [Fact]
    public void GetTeams_ReturnsKnownTeams()
    {
        var repo = new InMemoryCoachRepository();
        repo.Upsert(new CoachAssignment("Arsenal", "Arteta", D(2019, 12, 1)));
        repo.Upsert(new CoachAssignment("Man City", "Guardiola", D(2016, 7, 1)));
        var teams = repo.GetTeams();
        teams.Should().Contain(new[] { "Arsenal", "Man City" });
    }

    [Fact]
    public void GetCoach_TeamName_CaseInsensitive()
    {
        var repo = new InMemoryCoachRepository();
        repo.Upsert(new CoachAssignment("Arsenal", "Arteta", D(2019, 12, 1)));
        repo.GetCoach("arsenal", D(2024, 5, 1)).Should().Be("Arteta");
    }
}
