using FluentAssertions;
using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Services;
using FootballPrediction.Domain.Entities;
using Xunit;

namespace FootballPrediction.Tests.Application;

public class CoachServiceTests
{
    private static DateTime D(int y, int m, int d) => new(y, m, d);
    private static InMemoryCoachRepository Repo() => new();

    [Fact]
    public void UpdateCoach_SetsActiveCoach()
    {
        var svc = new CoachService(Repo());
        svc.UpdateCoach("Arsenal", "Arteta", D(2019, 12, 1));
        svc.GetCoach("Arsenal", D(2024, 5, 1)).Should().Be("Arteta");
    }

    [Fact]
    public void UpdateCoach_EmptyValues_AreIgnored()
    {
        var svc = new CoachService(Repo());
        svc.UpdateCoach("", "Arteta", D(2019, 12, 1));
        svc.UpdateCoach("Arsenal", "", D(2019, 12, 1));
        svc.GetCoach("Arsenal", D(2024, 5, 1)).Should().BeNull();
    }

    [Fact]
    public void UpdateCoach_NewCoach_ReplacesActive()
    {
        var svc = new CoachService(Repo());
        svc.UpdateCoach("Arsenal", "Arteta", D(2019, 12, 1));
        svc.UpdateCoach("Arsenal", "NewCoach", D(2025, 7, 1));
        svc.GetCoach("Arsenal", D(2024, 5, 1)).Should().Be("Arteta");
        svc.GetCoach("Arsenal", D(2025, 8, 1)).Should().Be("NewCoach");
    }

    [Fact]
    public void UpdateRange_AppliesAndCounts()
    {
        var svc = new CoachService(Repo());
        var updates = new List<CoachUpdate>
        {
            new() { Team = "Arsenal", Coach = "ArtetaA", EffectiveFrom = D(2019, 12, 1) },
            new() { Team = "Man City", Coach = "Guardiola", EffectiveFrom = D(2016, 7, 1) },
            new() { Team = "", Coach = "NoTeam", EffectiveFrom = D(2020, 1, 1) }, // ignoré
        };
        var applied = svc.UpdateRange(updates);
        applied.Should().Be(2);
        svc.GetCoach("Arsenal", D(2024, 1, 1)).Should().Be("ArtetaA");
    }

    [Fact]
    public void GetHistory_ReturnsAllAssignments()
    {
        var svc = new CoachService(Repo());
        svc.UpdateCoach("Arsenal", "Arteta", D(2019, 12, 1));
        svc.UpdateCoach("Arsenal", "NewCoach", D(2025, 7, 1));
        var hist = svc.GetHistory("Arsenal");
        hist.Should().HaveCount(2);
        hist.Select(a => a.Coach).Should().Contain(new[] { "Arteta", "NewCoach" });
    }

    [Fact]
    public void UpdateCoach_TeamName_IsTrimmed()
    {
        var svc = new CoachService(Repo());
        svc.UpdateCoach("  Arsenal  ", "Arteta", D(2019, 12, 1));
        svc.GetCoach("Arsenal", D(2024, 5, 1)).Should().Be("Arteta");
    }
}
