using FluentAssertions;
using FootballPrediction.Application.Services;
using FootballPrediction.Domain.Entities;
using Xunit;

namespace FootballPrediction.Tests.Application;

public class JsonCoachRepositoryTests
{
    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "coach_repo_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void EmptyFile_NoCoach_ReturnsNull()
    {
        var repo = new JsonCoachRepository(TempFile());
        repo.GetCoach("Arsenal", D(2024, 1, 1)).Should().BeNull();
    }

    [Fact]
    public void Upsert_PersistsAcrossInstances()
    {
        var path = TempFile();
        var repo1 = new JsonCoachRepository(path);
        repo1.Upsert(new CoachAssignment("Arsenal", "Arteta", D(2019, 12, 1)));
        File.Exists(path).Should().BeTrue();

        var repo2 = new JsonCoachRepository(path);
        repo2.GetCoach("Arsenal", D(2024, 5, 1)).Should().Be("Arteta");
    }

    [Fact]
    public void Upsert_NewCoach_ReplacesActiveAfterDate()
    {
        var path = TempFile();
        var repo = new JsonCoachRepository(path);
        repo.Upsert(new CoachAssignment("Arsenal", "Arteta", D(2019, 12, 1)));
        repo.Upsert(new CoachAssignment("Arsenal", "NewCoach", D(2025, 7, 1)));

        repo.GetCoach("Arsenal", D(2024, 5, 1)).Should().Be("Arteta");
        repo.GetCoach("Arsenal", D(2025, 8, 1)).Should().Be("NewCoach");
    }

    [Fact]
    public void Upsert_SameCoachSameDate_IsIdempotent()
    {
        var path = TempFile();
        var repo = new JsonCoachRepository(path);
        repo.Upsert(new CoachAssignment("Arsenal", "Arteta", D(2019, 12, 1)));
        repo.Upsert(new CoachAssignment("Arsenal", "Arteta", D(2019, 12, 1)));
        repo.GetHistory("Arsenal").Should().HaveCount(1);
    }
}
