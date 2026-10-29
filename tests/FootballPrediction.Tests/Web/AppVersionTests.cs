using FluentAssertions;
using FootballPrediction.Web.Services;
using Xunit;

namespace FootballPrediction.Tests.Web;

public class AppVersionTests
{
    [Theory]
    [InlineData(null, "dev")]
    [InlineData("", "dev")]
    [InlineData("  ", "dev")]
    [InlineData(" e3179f0 ", "e3179f0")]
    [InlineData("e3179f0-dirty", "e3179f0-dirty")]
    public void Resolve_falls_back_to_dev(string? input, string expected) =>
        AppVersion.Resolve(input).Should().Be(expected);
}
