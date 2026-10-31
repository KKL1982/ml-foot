using System.Globalization;
using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FootballPrediction.Application.Services;

/// <summary>Export PDF (A4 paysage) des prédictions batch, tableau paginé avec en-tête répété.</summary>
public sealed class PdfBatchExporter : IBatchExporter
{
    static PdfBatchExporter()
    {
        // Licence Community de QuestPDF (gratuite sous 1 M$ de chiffre d'affaires annuel).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string ContentType => "application/pdf";
    public string Extension => "pdf";

    public byte[] Export(IReadOnlyList<BatchPredictionItem> items, DateTime generatedAt)
    {
        var headers = new[] { "Date", "Championnat", "Domicile", "Extérieur", "Coachs (dom. / ext.)", "1X2", "P(1)", "P(X)", "P(2)", "Conf.", "Bet" };

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(t => t.FontSize(8));

            page.Header().Column(col =>
            {
                col.Item().Text("Prédictions de matchs").FontSize(16).Bold();
                col.Item().Text($"Généré le {generatedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} — {items.Count} match(s) — " +
                                $"HOME {items.Count(x => x.Bet == "HOME")} / AWAY {items.Count(x => x.Bet == "AWAY")} / SKIP {items.Count(x => x.Bet == "SKIP")}")
                    .FontColor(Colors.Grey.Darken1);
                col.Item().PaddingBottom(6);
            });

            page.Content().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(52);   // date
                    c.RelativeColumn(2);    // championnat
                    c.RelativeColumn(2.5f); // domicile
                    c.RelativeColumn(2.5f); // extérieur
                    c.RelativeColumn(3);    // coachs
                    c.ConstantColumn(28);   // 1X2
                    c.ConstantColumn(34);   // P1
                    c.ConstantColumn(34);   // PX
                    c.ConstantColumn(34);   // P2
                    c.ConstantColumn(34);   // conf
                    c.ConstantColumn(38);   // bet
                });

                table.Header(h =>
                {
                    foreach (var title in headers)
                        h.Cell().Background("#1F3A5F").Padding(3).Text(title).FontColor(Colors.White).Bold();
                });

                var n = 0;
                foreach (var i in items)
                {
                    var bg = n++ % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                    var coaches = string.IsNullOrWhiteSpace(i.HomeCoach) && string.IsNullOrWhiteSpace(i.AwayCoach)
                        ? "" : $"{i.HomeCoach} / {i.AwayCoach}";
                    var betColor = i.Bet == "HOME" ? "#C6EFCE" : i.Bet == "AWAY" ? "#BDD7EE" : bg;

                    void Cell(string text, string? background = null) =>
                        table.Cell().Background(background ?? bg).Padding(3).Text(text);

                    Cell(i.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    Cell(i.League);
                    Cell(i.HomeTeam);
                    Cell(i.AwayTeam);
                    Cell(coaches);
                    Cell(i.PredictedResult);
                    Cell(i.Probability1.ToString("P0", CultureInfo.InvariantCulture));
                    Cell(i.ProbabilityX.ToString("P0", CultureInfo.InvariantCulture));
                    Cell(i.Probability2.ToString("P0", CultureInfo.InvariantCulture));
                    Cell(i.Confidence.ToString("P0", CultureInfo.InvariantCulture));
                    Cell(i.Bet, betColor);
                }
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        })).GeneratePdf();
    }
}
