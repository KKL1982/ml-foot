using System.Globalization;
using System.Text;

namespace FootballPrediction.Application.Services;

/// <summary>
/// Convertit les noms d'équipes d'une source externe (ex. « Manchester City FC »)
/// vers les noms utilisés par le CSV et le modèle (ex. « Man City »).
/// Un nom non reconnu est renvoyé nettoyé (suffixes FC/AFC retirés) avec
/// <c>Known = false</c> pour que l'appelant puisse le signaler.
/// </summary>
public static class TeamNameMapper
{
    public readonly record struct Mapped(string Name, bool Known);

    // (nom source, nom canonique du CSV)
    private static readonly (string Api, string Canonical)[] Aliases =
    {
        // Premier League
        ("Arsenal FC", "Arsenal"), ("Aston Villa FC", "Aston Villa"), ("AFC Bournemouth", "Bournemouth"),
        ("Brentford FC", "Brentford"), ("Brighton & Hove Albion FC", "Brighton"), ("Burnley FC", "Burnley"),
        ("Chelsea FC", "Chelsea"), ("Crystal Palace FC", "Crystal Palace"), ("Everton FC", "Everton"),
        ("Fulham FC", "Fulham"), ("Ipswich Town FC", "Ipswich"), ("Leeds United FC", "Leeds"),
        ("Leicester City FC", "Leicester"), ("Liverpool FC", "Liverpool"), ("Manchester City FC", "Man City"),
        ("Manchester United FC", "Man United"), ("Newcastle United FC", "Newcastle"),
        ("Nottingham Forest FC", "Nott'm Forest"), ("Sunderland AFC", "Sunderland"),
        ("Tottenham Hotspur FC", "Tottenham"), ("West Ham United FC", "West Ham"),
        ("Wolverhampton Wanderers FC", "Wolves"), ("Southampton FC", "Southampton"),
        ("Luton Town FC", "Luton"), ("Sheffield United FC", "Sheffield United"),
        // LaLiga
        ("Deportivo Alavés", "Alaves"), ("UD Almería", "Almeria"), ("Athletic Club", "Ath Bilbao"),
        ("Club Atlético de Madrid", "Ath Madrid"), ("FC Barcelona", "Barcelona"), ("Real Betis Balompié", "Betis"),
        ("Cádiz CF", "Cadiz"), ("RC Celta de Vigo", "Celta"), ("RCD Espanyol de Barcelona", "Espanol"),
        ("Getafe CF", "Getafe"), ("Girona FC", "Girona"), ("Granada CF", "Granada"), ("UD Las Palmas", "Las Palmas"),
        ("CD Leganés", "Leganes"), ("RCD Mallorca", "Mallorca"), ("CA Osasuna", "Osasuna"),
        ("Real Madrid CF", "Real Madrid"), ("Sevilla FC", "Sevilla"), ("Real Sociedad de Fútbol", "Sociedad"),
        ("Valencia CF", "Valencia"), ("Real Valladolid CF", "Valladolid"), ("Rayo Vallecano de Madrid", "Vallecano"),
        ("Villarreal CF", "Villarreal"), ("Elche CF", "Elche"), ("Levante UD", "Levante"), ("Real Oviedo", "Oviedo"),
        // Bundesliga
        ("FC Augsburg", "Augsburg"), ("FC Bayern München", "Bayern Munich"), ("VfL Bochum 1848", "Bochum"),
        ("SV Darmstadt 98", "Darmstadt"), ("Borussia Dortmund", "Dortmund"), ("Eintracht Frankfurt", "Ein Frankfurt"),
        ("1. FC Köln", "FC Koln"), ("SC Freiburg", "Freiburg"), ("1. FC Heidenheim 1846", "Heidenheim"),
        ("TSG 1899 Hoffenheim", "Hoffenheim"), ("Holstein Kiel", "Holstein Kiel"), ("Bayer 04 Leverkusen", "Leverkusen"),
        ("Borussia Mönchengladbach", "M'gladbach"), ("1. FSV Mainz 05", "Mainz"), ("RB Leipzig", "RB Leipzig"),
        ("FC St. Pauli 1910", "St Pauli"), ("VfB Stuttgart", "Stuttgart"), ("1. FC Union Berlin", "Union Berlin"),
        ("SV Werder Bremen", "Werder Bremen"), ("VfL Wolfsburg", "Wolfsburg"), ("Hamburger SV", "Hamburg"),
        // Ligue 1
        ("Angers SCO", "Angers"), ("AJ Auxerre", "Auxerre"), ("Stade Brestois 29", "Brest"),
        ("Clermont Foot 63", "Clermont"), ("Le Havre AC", "Le Havre"), ("RC Lens", "Lens"), ("Lille OSC", "Lille"),
        ("FC Lorient", "Lorient"), ("Olympique Lyonnais", "Lyon"), ("Olympique de Marseille", "Marseille"),
        ("FC Metz", "Metz"), ("AS Monaco FC", "Monaco"), ("Montpellier HSC", "Montpellier"), ("FC Nantes", "Nantes"),
        ("OGC Nice", "Nice"), ("Paris Saint-Germain FC", "Paris SG"), ("Stade de Reims", "Reims"),
        ("Stade Rennais FC", "Rennes"), ("AS Saint-Étienne", "St Etienne"), ("RC Strasbourg Alsace", "Strasbourg"),
        ("Toulouse FC", "Toulouse"), ("Paris FC", "Paris FC"),
        // Variantes renvoyées par l'API
        ("Racing Club de Lens", "Lens"), ("Stade Rennais FC 1901", "Rennes"),
        // Promus / équipes absentes des données d'entraînement (noms style football-data.co.uk)
        ("Coventry City FC", "Coventry"), ("Hull City AFC", "Hull"), ("FC Schalke 04", "Schalke 04"),
        ("SC Paderborn 07", "Paderborn"), ("SV 07 Elversberg", "Elversberg"), ("ES Troyes AC", "Troyes"),
        ("Le Mans FC", "Le Mans"), ("Málaga CF", "Malaga"), ("RC Deportivo La Coruña", "La Coruna"),
        ("Real Racing Club de Santander", "Santander"),
        // Variantes de The Odds API
        ("Brighton and Hove Albion", "Brighton"), ("Wolverhampton Wanderers", "Wolves"),
        ("Atletico Madrid", "Ath Madrid"), ("Athletic Bilbao", "Ath Bilbao"), ("Celta Vigo", "Celta"),
        ("Real Betis", "Betis"), ("Real Sociedad", "Sociedad"), ("Espanyol", "Espanol"), ("Alavés", "Alaves"),
        ("Rayo Vallecano", "Vallecano"), ("Oviedo", "Oviedo"),
        ("Bayern Munich", "Bayern Munich"), ("Bayer Leverkusen", "Leverkusen"), ("FSV Mainz 05", "Mainz"),
        ("FC St. Pauli", "St Pauli"), ("TSG Hoffenheim", "Hoffenheim"), ("Borussia Monchengladbach", "M'gladbach"),
        ("FC Cologne", "FC Koln"),
        ("Paris Saint Germain", "Paris SG"), ("Paris Saint-Germain", "Paris SG"), ("Olympique Marseille", "Marseille"),
        ("Stade Rennes", "Rennes"), ("Saint-Etienne", "St Etienne"), ("Racing Lens", "Lens"),
        ("Ipswich Town", "Ipswich"), ("Leeds United", "Leeds"), ("Newcastle United", "Newcastle"),
        ("Tottenham Hotspur", "Tottenham"), ("West Ham United", "West Ham"), ("Manchester City", "Man City"),
        ("Manchester United", "Man United"), ("Nottingham Forest", "Nott'm Forest"), ("Leicester City", "Leicester"),
    };

    private static readonly Dictionary<string, string> ByApiName = BuildByApiName();
    private static readonly Dictionary<string, string> ByCanonical = BuildByCanonical();

    public static Mapped Map(string? sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            return new Mapped(string.Empty, false);

        var key = Normalize(sourceName);
        if (ByApiName.TryGetValue(key, out var canonical)
            || ByApiName.TryGetValue(StripKeySuffix(key), out canonical))
            return new Mapped(canonical, true);
        if (ByCanonical.TryGetValue(key, out canonical))
            return new Mapped(canonical, true);

        return new Mapped(StripClubSuffix(sourceName.Trim()), false);
    }

    private static Dictionary<string, string> BuildByApiName()
    {
        var d = new Dictionary<string, string>();
        foreach (var (api, canonical) in Aliases)
        {
            var key = Normalize(api);
            d[key] = canonical;
            var bare = StripKeySuffix(key);
            if (bare.Length >= 4) d.TryAdd(bare, canonical);
        }
        return d;
    }

    private static Dictionary<string, string> BuildByCanonical()
    {
        var d = new Dictionary<string, string>();
        foreach (var (_, canonical) in Aliases)
            d[Normalize(canonical)] = canonical;
        return d;
    }

    private static string StripKeySuffix(string key)
    {
        foreach (var suffix in new[] { "afc", "fc", "cf" })
            if (key.Length > suffix.Length + 3 && key.EndsWith(suffix, StringComparison.Ordinal))
                return key[..^suffix.Length];
        return key;
    }

    private static string StripClubSuffix(string name)
    {
        foreach (var suffix in new[] { " FC", " AFC", " CF" })
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return name[..^suffix.Length].Trim();
        return name;
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
