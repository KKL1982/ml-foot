using System.Globalization;
using FluentAssertions;
using FootballPrediction.Application.Services;
using FootballPrediction.Domain.Enums;
using Xunit;

namespace FootballPrediction.Tests.Application;

public class CsvParserServiceTests
{
    [Fact]
    public async Task ParseMatchesAsync_ValidCsv_ShouldParseCorrectly()
    {
        var csvContent = @"Date,League,Season,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Result,HomeCoach,AwayCoach,Bet365_1,Bet365_X,Bet365_2,Pinnacle_1,Pinnacle_X,Pinnacle_2,WilliamHill_1,WilliamHill_X,WilliamHill_2
2024-08-16,Premier League,2024-2025,Manchester United,Fulham,1,0,1,Ten Hag,Silva,1.44,4.75,6.50,1.48,4.60,6.20,1.45,4.50,6.00
2024-08-17,Premier League,2024-2025,Ipswich,Liverpool,0,2,2,McKenna,Slot,9.00,5.50,1.30,9.50,5.80,1.27,,,,";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, csvContent);

        try
        {
            var parser = new CsvParserService();
            var matches = await parser.ParseMatchesAsync(tempFile);

            matches.Should().HaveCount(2);
            matches[0].HomeTeam.Should().Be("Manchester United");
            matches[0].AwayTeam.Should().Be("Fulham");
            matches[0].HomeGoals.Should().Be(1);
            matches[0].AwayGoals.Should().Be(0);
            matches[0].Result.Should().Be(MatchResult.HomeWin);
            matches[0].BookmakerOdds.Should().ContainKey("Bet365");
            matches[0].BookmakerOdds["Bet365"].HomeWin.Should().Be(1.44m);

            matches[1].HomeTeam.Should().Be("Ipswich");
            matches[1].Result.Should().Be(MatchResult.AwayWin);

            // WilliamHill should not be added for row 2 (empty)
            matches[1].BookmakerOdds.Should().NotContainKey("WilliamHill");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseMatchesAsync_DrawResult_ShouldParseCorrectly()
    {
        var csvContent = @"Date,League,Season,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Result
2024-08-17,Premier League,2024-2025,Chelsea,Arsenal,1,1,X";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, csvContent);

        try
        {
            var parser = new CsvParserService();
            var matches = await parser.ParseMatchesAsync(tempFile);

            matches.Should().HaveCount(1);
            matches[0].Result.Should().Be(MatchResult.Draw);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseMatchesAsync_EmptyFile_ShouldReturnEmptyList()
    {
        var csvContent = @"Date,League,Season,HomeTeam,AwayTeam";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, csvContent);

        try
        {
            var parser = new CsvParserService();
            var matches = await parser.ParseMatchesAsync(tempFile);

            matches.Should().BeEmpty();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
