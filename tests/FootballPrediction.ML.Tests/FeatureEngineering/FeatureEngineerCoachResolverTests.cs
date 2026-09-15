using FluentAssertions;
using FootballPrediction.Domain.Entities;
using FootballPrediction.Domain.Enums;
using FootballPrediction.ML.FeatureEngineering;
using Xunit;

namespace FootballPrediction.ML.Tests.FeatureEngineering;

public class FeatureEngineerCoachResolverTests
{
    private static Match MakeMatch(string homeTeam, string awayTeam, DateTime date, string? homeCoach, string? awayCoach)
    {
        return new Match
        {
            Date = date,
            League = "Premier League",
            Season = "2024-2025",
            HomeTeam = homeTeam,
            AwayTeam = awayTeam,
            HomeGoals = 1,
            AwayGoals = 0,
            Result = MatchResult.HomeWin,
            HomeCoach = homeCoach,
            AwayCoach = awayCoach,
        };
    }

    [Fact]
    public void BuildFeatures_WithResolver_AppliesUpdatedCoach()
    {
        var matches = new List<Match>
        {
            MakeMatch("Arsenal", "Chelsea", new DateTime(2024, 8, 1), "OldCoach", null),
            MakeMatch("Arsenal", "Liverpool", new DateTime(2024, 9, 1), "OldCoach", null),
        };

        // Resolver remplace le coach figé du CSV par le coach à jour.
        var features = FeatureEngineer.BuildFeatures(matches, binaryMode: false, coachResolver: (team, date)
            => team == "Arsenal" ? "NewCoach" : null);

        features.Should().HaveCount(2);
        features.All(f => f.HomeCoach == "NewCoach").Should().BeTrue();
    }

    [Fact]
    public void BuildFeatures_WithResolver_NewCoachTenureResetsToZero()
    {
        // Deux matchs Arsenal : le resolver retourne NewCoach dans les deux cas.
        // Le 1er match → tenure 0, le 2e → tenure 1.
        var matches = new List<Match>
        {
            MakeMatch("Arsenal", "Chelsea", new DateTime(2024, 8, 1), "OldCoach", null),
            MakeMatch("Arsenal", "Liverpool", new DateTime(2024, 9, 1), "OldCoach", null),
        };

        var features = FeatureEngineer.BuildFeatures(matches, binaryMode: false, coachResolver: (team, date)
            => team == "Arsenal" ? "NewCoach" : null);

        features[0].HomeCoachTenure.Should().Be(0f);
        features[1].HomeCoachTenure.Should().Be(1f);
    }

    [Fact]
    public void BuildFeatures_WithoutResolver_PreservesCsvCoach()
    {
        var matches = new List<Match>
        {
            MakeMatch("Arsenal", "Chelsea", new DateTime(2024, 8, 1), "CSVCoachA", "CSVCoachB"),
        };

        var features = FeatureEngineer.BuildFeatures(matches);
        features.Single().HomeCoach.Should().Be("CSVCoachA");
        features.Single().AwayCoach.Should().Be("CSVCoachB");
    }

    [Fact]
    public void BuildFeatures_ResolverReturnsNull_FallsBackToCsvCoach()
    {
        var matches = new List<Match>
        {
            MakeMatch("Arsenal", "Chelsea", new DateTime(2024, 8, 1), "CSVCoachA", "CSVCoachB"),
        };

        var features = FeatureEngineer.BuildFeatures(matches, binaryMode: false, coachResolver: (team, date) => null);

        features.Single().HomeCoach.Should().Be("CSVCoachA");
        features.Single().AwayCoach.Should().Be("CSVCoachB");
    }
}
