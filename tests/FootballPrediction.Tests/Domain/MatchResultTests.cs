using FluentAssertions;
using FootballPrediction.Domain.Enums;
using Xunit;

namespace FootballPrediction.Tests.Domain;

public class MatchResultTests
{
    [Theory]
    [InlineData(2, 1, MatchResult.HomeWin)]
    [InlineData(1, 1, MatchResult.Draw)]
    [InlineData(0, 3, MatchResult.AwayWin)]
    [InlineData(5, 0, MatchResult.HomeWin)]
    [InlineData(0, 0, MatchResult.Draw)]
    public void FromScore_ShouldReturnCorrectResult(int homeGoals, int awayGoals, MatchResult expected)
    {
        var result = MatchResultExtensions.FromScore(homeGoals, awayGoals);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(MatchResult.HomeWin, "1")]
    [InlineData(MatchResult.Draw, "X")]
    [InlineData(MatchResult.AwayWin, "2")]
    public void ToDisplayString_ShouldReturnCorrectValue(MatchResult result, string expected)
    {
        result.ToDisplayString().Should().Be(expected);
    }
}
