using System.Text.RegularExpressions;
using FootballPrediction.Domain.Entities;
using Match = FootballPrediction.Domain.Entities.Match;

namespace FootballPrediction.Application.Services;

public sealed record MatchFileInfo(string FileName, DateTime CreatedUtc, int MatchCount);

/// <summary>
/// Dossier des CSV de matchs créés par l'application (ex. /app/data/matches).
/// Les fichiers restent disponibles pour « Prédire à nouveau » et le téléchargement.
/// </summary>
public sealed class MatchFileStore
{
    private static readonly Regex SafeName = new(@"^[A-Za-z0-9._-]+\.csv$", RegexOptions.Compiled);
    private readonly string _directory;

    public MatchFileStore(string directory)
    {
        _directory = directory;
    }

    public async Task<string> SaveAsync(string fileName, IEnumerable<Match> matches)
    {
        var path = ResolvePath(fileName) ?? throw new ArgumentException("Nom de fichier invalide.", nameof(fileName));
        await MatchCsvWriter.WriteAsync(path, matches);
        return path;
    }

    /// <summary>Chemin complet d'un fichier existant, ou null (nom invalide, absent).</summary>
    public string? FindExisting(string? fileName)
    {
        var path = ResolvePath(fileName);
        return path != null && File.Exists(path) ? path : null;
    }

    public IReadOnlyList<MatchFileInfo> List()
    {
        if (!Directory.Exists(_directory))
            return Array.Empty<MatchFileInfo>();

        return Directory.EnumerateFiles(_directory, "*.csv")
            .Where(f => SafeName.IsMatch(Path.GetFileName(f)))
            .Select(f => new MatchFileInfo(
                Path.GetFileName(f),
                File.GetLastWriteTimeUtc(f),
                Math.Max(0, File.ReadLines(f).Count() - 1)))
            .OrderByDescending(i => i.CreatedUtc)
            .ToList();
    }

    private string? ResolvePath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !SafeName.IsMatch(fileName) || fileName.Contains(".."))
            return null;
        return Path.Combine(_directory, fileName);
    }
}
