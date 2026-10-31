using ClosedXML.Excel;
using FluentAssertions;
using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Services;
using Xunit;

namespace FootballPrediction.Tests.Application;

public class BatchExportersTests
{
    private static BatchPredictionItem Item(string bet = "HOME") => new()
    {
        Date = new DateTime(2024, 1, 1), League = "PL", HomeTeam = "Arsenal", AwayTeam = "Chelsea",
        HomeCoach = "Arteta", AwayCoach = "Maresca", PredictedResult = "1",
        Probability1 = 0.6, ProbabilityX = 0.25, Probability2 = 0.15, Confidence = 0.6, Bet = bet, Comment = "ok"
    };

    [Fact]
    public void Xlsx_ContainsHeadersRowsAndBet()
    {
        var bytes = new XlsxBatchExporter().Export(new[] { Item("HOME"), Item("SKIP") }, DateTime.Now);

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet("Prédictions");
        ws.Cell(1, 1).GetString().Should().Be("Date");
        ws.Cell(2, 3).GetString().Should().Be("Arsenal");
        ws.Cell(2, 12).GetString().Should().Be("HOME");
        ws.Cell(3, 12).GetString().Should().Be("SKIP");
        ws.Cell(2, 8).GetDouble().Should().BeApproximately(0.6, 1e-9);
        ws.LastRowUsed()!.RowNumber().Should().Be(3);
    }

    [Fact]
    public void Xlsx_EmptyList_DoesNotThrow()
    {
        var bytes = new XlsxBatchExporter().Export(Array.Empty<BatchPredictionItem>(), DateTime.Now);
        bytes.Should().NotBeEmpty();
    }

    [Fact]
    public void Pdf_StartsWithPdfSignature_AndHandlesLargeAndEmptyLists()
    {
        var exporter = new PdfBatchExporter();

        var pdf = exporter.Export(Enumerable.Range(0, 300).Select(_ => Item()).ToList(), DateTime.Now);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");

        exporter.Export(Array.Empty<BatchPredictionItem>(), DateTime.Now).Should().NotBeEmpty();
    }
}
