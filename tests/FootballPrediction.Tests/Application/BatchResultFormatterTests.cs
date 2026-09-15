using FluentAssertions;
using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Services;
using Xunit;

namespace FootballPrediction.Tests.Application;

public class BatchResultFormatterTests
{
    private static BatchPredictionItem Item(DateTime date = default, string result = "1")
    {
        return new BatchPredictionItem
        {
            Date = date == default ? new DateTime(2026, 9, 20) : date,
            League = "Premier League",
            HomeTeam = "Arsenal",
            AwayTeam = "Chelsea",
            HomeCoach = "Arteta",
            AwayCoach = "",
            PredictedResult = result,
            Probability1 = 0.60, ProbabilityX = 0.25, Probability2 = 0.15,
            Confidence = 0.60,
            Bet = "HOME",
            Comment = "Home win likely",
        };
    }

    [Fact]
    public void ToCsv_Empty_ReturnsOnlyHeader()
    {
        var csv = BatchResultFormatter.ToCsv(Array.Empty<BatchPredictionItem>());
        csv.Trim().Should().Be(BatchResultFormatter.CsvHeader);
    }

    [Fact]
    public void ToCsv_OneItem_RowsMatchColumns()
    {
        var csv = BatchResultFormatter.ToCsv(new[] { Item() });
        var lines = csv.Trim().Split('\n');
        lines.Should().HaveCount(2);
        var cells = lines[1].Split(',');
        cells[0].Should().Be("2026-09-20");
        cells[1].Should().Be("Premier League");
        cells[2].Should().Be("Arsenal");
        cells[3].Should().Be("Chelsea");
        cells[4].Should().Be("Arteta");
        cells[6].Should().Be("1");
        cells[11].Should().Be("HOME");
        cells[12].Should().Be("Home win likely");
    }

    [Fact]
    public void ToConsoleTable_Empty_ContainsOnlyHeaderLines()
    {
        var table = BatchResultFormatter.ToConsoleTable(Array.Empty<BatchPredictionItem>());
        table.Trim().Should().NotBeEmpty();
        table.Should().Contain("1X2");
    }

    [Fact]
    public void ToConsoleTable_ContainsTeams()
    {
        var table = BatchResultFormatter.ToConsoleTable(new[] { Item() });
        table.Should().Contain("Arsenal");
        table.Should().Contain("Chelsea");
    }
}
