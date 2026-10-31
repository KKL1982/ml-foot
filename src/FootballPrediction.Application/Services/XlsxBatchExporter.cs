using ClosedXML.Excel;
using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;

namespace FootballPrediction.Application.Services;

/// <summary>Export Excel (.xlsx) des prédictions batch : une feuille de données + une feuille de résumé.</summary>
public sealed class XlsxBatchExporter : IBatchExporter
{
    public static readonly string[] Headers =
    {
        "Date", "Championnat", "Domicile", "Extérieur", "Coach dom.", "Coach ext.",
        "Prédiction", "P(1)", "P(X)", "P(2)", "Confiance", "Bet", "Commentaire"
    };

    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string Extension => "xlsx";

    public byte[] Export(IReadOnlyList<BatchPredictionItem> items, DateTime generatedAt)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Prédictions");

        for (var c = 0; c < Headers.Length; c++)
            ws.Cell(1, c + 1).Value = Headers[c];
        var header = ws.Range(1, 1, 1, Headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F3A5F");
        header.Style.Font.FontColor = XLColor.White;

        for (var r = 0; r < items.Count; r++)
        {
            var i = items[r];
            var row = r + 2;
            ws.Cell(row, 1).Value = i.Date.Date;
            ws.Cell(row, 2).Value = i.League;
            ws.Cell(row, 3).Value = i.HomeTeam;
            ws.Cell(row, 4).Value = i.AwayTeam;
            ws.Cell(row, 5).Value = i.HomeCoach ?? string.Empty;
            ws.Cell(row, 6).Value = i.AwayCoach ?? string.Empty;
            ws.Cell(row, 7).Value = i.PredictedResult;
            ws.Cell(row, 8).Value = i.Probability1;
            ws.Cell(row, 9).Value = i.ProbabilityX;
            ws.Cell(row, 10).Value = i.Probability2;
            ws.Cell(row, 11).Value = i.Confidence;
            ws.Cell(row, 12).Value = i.Bet;
            ws.Cell(row, 13).Value = i.Comment;

            var bet = ws.Cell(row, 12);
            if (i.Bet == "HOME") bet.Style.Fill.BackgroundColor = XLColor.FromHtml("#C6EFCE");
            else if (i.Bet == "AWAY") bet.Style.Fill.BackgroundColor = XLColor.FromHtml("#BDD7EE");
            else bet.Style.Font.FontColor = XLColor.Gray;
        }

        if (items.Count > 0)
        {
            ws.Range(2, 1, items.Count + 1, 1).Style.DateFormat.Format = "yyyy-mm-dd";
            ws.Range(2, 8, items.Count + 1, 11).Style.NumberFormat.Format = "0.0%";
            ws.Range(1, 1, items.Count + 1, Headers.Length).SetAutoFilter();
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, Math.Min(items.Count + 1, 200));

        var summary = wb.Worksheets.Add("Résumé");
        summary.Cell(1, 1).Value = "Généré le";
        summary.Cell(1, 2).Value = generatedAt;
        summary.Cell(1, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        summary.Cell(2, 1).Value = "Matchs prédits";
        summary.Cell(2, 2).Value = items.Count;
        summary.Cell(3, 1).Value = "Paris HOME";
        summary.Cell(3, 2).Value = items.Count(x => x.Bet == "HOME");
        summary.Cell(4, 1).Value = "Paris AWAY";
        summary.Cell(4, 2).Value = items.Count(x => x.Bet == "AWAY");
        summary.Cell(5, 1).Value = "SKIP";
        summary.Cell(5, 2).Value = items.Count(x => x.Bet == "SKIP");
        summary.Cell(6, 1).Value = "Confiance moyenne";
        summary.Cell(6, 2).Value = items.Count == 0 ? 0 : items.Average(x => x.Confidence);
        summary.Cell(6, 2).Style.NumberFormat.Format = "0.0%";
        summary.Column(1).Style.Font.Bold = true;
        summary.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
