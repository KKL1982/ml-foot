using System.Globalization;
using System.Text;
using FootballPrediction.Domain.Entities;
using FootballPrediction.Domain.Enums;

namespace FootballPrediction.Application.Services;

/// <summary>
/// Écrit des matchs au format CSV attendu par <see cref="CsvParserService"/>
/// (même format que l'upload et que la commande CLI load-matches), afin que
/// le fichier créé soit directement réutilisable pour la prédiction.
/// </summary>
public static class MatchCsvWriter
{
    public const string Header = "Date,League,Season,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Result,HomeCoach,AwayCoach";

    public static string ToCsv(IEnumerable<Match> matches)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        foreach (var m in matches)
        {
            var result = m.Result switch
            {
                MatchResult.HomeWin => "1",
                MatchResult.Draw => "X",
                MatchResult.AwayWin => "2",
                _ => ""
            };
            sb.AppendLine(string.Join(",",
                m.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Escape(m.League), Escape(m.Season),
                Escape(m.HomeTeam), Escape(m.AwayTeam),
                m.HomeGoals?.ToString() ?? "", m.AwayGoals?.ToString() ?? "",
                result, Escape(m.HomeCoach), Escape(m.AwayCoach)));
        }
        return sb.ToString();
    }

    public static async Task WriteAsync(string path, IEnumerable<Match> matches)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, ToCsv(matches), new UTF8Encoding(false));
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
