using System.Text;
using FootballPrediction.Application.DTOs;

namespace FootballPrediction.Application.Services;

/// <summary>
/// Formate une liste de <see cref="BatchPredictionItem"/> pour l'affichage en
/// tableau (console) et l'export CSV. Logique de présentation pure, testable
/// et réutilisée par le CLI et le Web.
/// </summary>
public static class BatchResultFormatter
{
    public const string CsvHeader = "Date,League,HomeTeam,AwayTeam,HomeCoach,AwayCoach,PredictedResult,Probability1,ProbabilityX,Probability2,Confidence,Bet,Comment";

    /// <summary>Génère le contenu CSV de la table de prédictions.</summary>
    public static string ToCsv(IReadOnlyList<BatchPredictionItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CsvHeader);
        foreach (var i in items)
        {
            sb.AppendLine(string.Join(",",
                i.Date.ToString("yyyy-MM-dd"), Escape(i.League),
                Escape(i.HomeTeam), Escape(i.AwayTeam),
                Escape(i.HomeCoach), Escape(i.AwayCoach),
                Escape(i.PredictedResult),
                i.Probability1.ToString("F3"), i.ProbabilityX.ToString("F3"), i.Probability2.ToString("F3"),
                i.Confidence.ToString("F3"), i.Bet, Escape(i.Comment)));
        }
        return sb.ToString();
    }

    /// <summary>Génère un tableau texte aligné (largeur fixe) pour la console.</summary>
    public static string ToConsoleTable(IReadOnlyList<BatchPredictionItem> items)
    {
        var sb = new StringBuilder();
        string[] headers = { "Date", "Championnat", "Domicile", "Extérieur", "CoachDom", "CoachExt", "1X2", "1", "X", "2", "Conf", "Bet" };
        int[] widths = { 10, 12, 14, 14, 10, 10, 4, 6, 6, 6, 6, 5 };

        var rows = items.Select(i => new string[]
        {
            i.Date.ToString("yyyy-MM-dd"), Truncate(i.League, widths[1]),
            Truncate(i.HomeTeam, widths[2]), Truncate(i.AwayTeam, widths[3]),
            Truncate(i.HomeCoach, widths[4]), Truncate(i.AwayCoach, widths[5]),
            i.PredictedResult, i.Probability1.ToString("P0"), i.ProbabilityX.ToString("P0"),
            i.Probability2.ToString("P0"), i.Confidence.ToString("P0"), i.Bet,
        }).ToList();

        sb.AppendLine("  " + string.Join("  ", headers.Select((h, c) => Pad(h, widths[c]))));
        sb.AppendLine("  " + string.Join("  ", widths.Select(w => new string('-', w))));
        foreach (var r in rows)
            sb.AppendLine("  " + string.Join("  ", r.Select((cell, c) => Pad(cell, widths[c]))));
        return sb.ToString();
    }

    private static string Pad(string s, int w) => s.Length >= w ? s.Substring(0, w) : s + new string(' ', w - s.Length);

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? new string(' ', max) : (s.Length > max ? s.Substring(0, max - 1) + "…" : s);

    private static string Escape(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Contains(',') ? $"\"{value}\"" : value;
}
