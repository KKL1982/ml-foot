using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Domain.Entities;
using FootballPrediction.Domain.Enums;
using FootballPrediction.Domain.ValueObjects;

namespace FootballPrediction.Application.Services;

public class CsvParserService : ICsvParser
{
    public async Task<IReadOnlyList<Match>> ParseMatchesAsync(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            PrepareHeaderForMatch = args => args.Header.Trim()
        });

        var records = new List<Match>();
        await foreach (var record in csv.GetRecordsAsync<MatchCsvRow>())
        {
            if (!string.IsNullOrWhiteSpace(record.Date))
                records.Add(MapToMatch(record));
        }

        return records;
    }

    private static Match MapToMatch(MatchCsvRow row)
    {
        var match = new Match
        {
            League = row.League ?? string.Empty,
            Season = row.Season ?? string.Empty,
            HomeTeam = row.HomeTeam ?? string.Empty,
            AwayTeam = row.AwayTeam ?? string.Empty,
            HomeCoach = row.HomeCoach,
            AwayCoach = row.AwayCoach
        };

        if (TryParseMatchDate(row.Date, out var date))
            match.Date = date;

        if (int.TryParse(row.HomeGoals, out var hg))
            match.HomeGoals = hg;
        if (int.TryParse(row.AwayGoals, out var ag))
            match.AwayGoals = ag;

        match.Result = ParseResult(row.Result);
        match.BookmakerOdds = ParseOdds(row);

        return match;
    }

    private static MatchResult? ParseResult(string? result) => result?.Trim().ToUpperInvariant() switch
    {
        "1" or "H" => MatchResult.HomeWin,
        "X" or "D" or "N" => MatchResult.Draw,
        "2" or "A" => MatchResult.AwayWin,
        _ => null
    };

    /// <summary>
    /// Parse une date de match en acceptant plusieurs formats :
    /// ISO (yyyy-MM-dd), français (dd/MM/yyyy), français avec heure, ou ISO complet.
    /// Le format français est privilégié (celui du CSV de référence).
    /// </summary>
    private static bool TryParseMatchDate(string? value, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var s = value.Trim();

        // Formats avec séparateur '/': on force la lecture en jour/mois/année (dd/MM/yyyy).
        // NB : les CSV mêlent année 4 chiffres (dd/MM/yyyy) et 2 chiffres (dd/MM/yy) selon les saisons.
        if (s.Contains('/'))
        {
            if (DateTime.TryParseExact(s, new[]
                {
                    "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy",
                    "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm", "dd/MM/yy HH:mm", "d/M/yy H:mm"
                }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;
            // Repli : TryParse classique (peut gérer d'autres séparateurs).
            return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        // Format ISO (yyyy-MM-dd) ou autres.
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static Dictionary<string, Odds> ParseOdds(MatchCsvRow row)
    {
        var odds = new Dictionary<string, Odds>();

        void TryAdd(string bookmaker, string? h, string? d, string? a)
        {
            if (decimal.TryParse(h, NumberStyles.Any, CultureInfo.InvariantCulture, out var home) &&
                decimal.TryParse(d, NumberStyles.Any, CultureInfo.InvariantCulture, out var draw) &&
                decimal.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out var away) &&
                home > 0 && draw > 0 && away > 0)
            {
                odds[bookmaker] = new Odds { Bookmaker = bookmaker, HomeWin = home, Draw = draw, AwayWin = away };
            }
        }

        TryAdd("Bet365", row.Bet365_1, row.Bet365_X, row.Bet365_2);
        TryAdd("Pinnacle", row.Pinnacle_1, row.Pinnacle_X, row.Pinnacle_2);
        TryAdd("WilliamHill", row.WilliamHill_1, row.WilliamHill_X, row.WilliamHill_2);

        return odds;
    }
}
