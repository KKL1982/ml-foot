using FluentAssertions;
using FootballPrediction.ML.FeatureEngineering;
using Xunit;

namespace FootballPrediction.ML.Tests.FeatureEngineering;

public class OddsNormalizerTests
{
    [Fact]
    public void Normalize_ShouldReturnProbabilitiesSummingToOne()
    {
        var (home, draw, away) = OddsNormalizer.Normalize(2.0, 3.5, 4.0);

        (home + draw + away).Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void Normalize_LowOdds_ShouldYieldHigherProbability()
    {
        var (home1, _, _) = OddsNormalizer.Normalize(1.5, 3.0, 3.0);
        var (home2, _, _) = OddsNormalizer.Normalize(6.0, 3.0, 3.0);

        home1.Should().BeGreaterThan(home2);
    }

    [Theory]
    [InlineData(0, 2, 2)]
    [InlineData(2, 0, 2)]
    [InlineData(2, 2, 0)]
    [InlineData(-1, 2, 2)]
    public void Normalize_InvalidOdds_ShouldThrow(double h, double d, double a)
    {
        Action act = () => OddsNormalizer.Normalize(h, d, a);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HomeVsAwayProbabilityGap_FavoriteHome_ShouldBePositive()
    {
        var gap = OddsNormalizer.HomeVsAwayProbabilityGap(1.5, 6.0);
        gap.Should().BeGreaterThan(0);
    }

    [Fact]
    public void HomeVsAwayProbabilityGap_FavoriteAway_ShouldBeNegative()
    {
        var gap = OddsNormalizer.HomeVsAwayProbabilityGap(6.0, 1.5);
        gap.Should().BeLessThan(0);
    }
}
