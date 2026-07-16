using FluentAssertions;
using FootballPrediction.Domain.ValueObjects;
using Xunit;

namespace FootballPrediction.Tests.Domain;

public class ImpliedProbabilityTests
{
    [Fact]
    public void FromOdds_ShouldNormalizeCorrectly()
    {
        var result = ImpliedProbability.FromOdds(2.0, 3.5, 4.0);

        result.HomeWin.Should().BeApproximately(0.482759, 0.0001);
        result.Draw.Should().BeApproximately(0.275862, 0.0001);
        result.AwayWin.Should().BeApproximately(0.241379, 0.0001);
    }

    [Fact]
    public void FromOdds_ShouldSumToOne()
    {
        var result = ImpliedProbability.FromOdds(1.5, 4.0, 6.0);
        result.Sum.Should().BeApproximately(1.0, 0.0001);
    }

    [Theory]
    [InlineData(0, 2.0, 2.0)]
    [InlineData(2.0, 0, 2.0)]
    [InlineData(2.0, 2.0, 0)]
    [InlineData(-1, 2.0, 2.0)]
    public void FromOdds_WithInvalidOdds_ShouldThrow(double h, double d, double a)
    {
        Action act = () => ImpliedProbability.FromOdds(h, d, a);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromOdds_EqualOdds_ShouldYieldEqualProbabilities()
    {
        var result = ImpliedProbability.FromOdds(2.0, 2.0, 2.0);
        result.HomeWin.Should().BeApproximately(1.0 / 3, 0.0001);
        result.Draw.Should().BeApproximately(1.0 / 3, 0.0001);
        result.AwayWin.Should().BeApproximately(1.0 / 3, 0.0001);
    }
}
