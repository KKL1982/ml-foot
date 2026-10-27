using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Application.Services;
using FootballPrediction.Domain.Entities;
using FootballPrediction.Web.Models;
using FootballPrediction.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using Match = FootballPrediction.Domain.Entities.Match;

namespace FootballPrediction.Web.Controllers;

[Authorize]
public class BatchController : Controller
{
    private const int MaxPeriodDays = 31;

    private readonly IPredictionService _predictionService;
    private readonly BatchPredictionService _batchPredictionService;
    private readonly ICsvParser _csvParser;
    private readonly ModelSettings _settings;
    private readonly ILogger<BatchController> _logger;
    private readonly MatchFetchService? _matchFetchService;
    private readonly CoachRefreshService? _coachRefreshService;
    private readonly MatchFileStore? _fileStore;

    public BatchController(
        IPredictionService predictionService,
        BatchPredictionService batchPredictionService,
        ICsvParser csvParser,
        IOptions<ModelSettings> options,
        ILogger<BatchController> logger,
        MatchFetchService? matchFetchService = null,
        CoachRefreshService? coachRefreshService = null,
        MatchFileStore? fileStore = null)
    {
        _predictionService = predictionService;
        _batchPredictionService = batchPredictionService;
        _csvParser = csvParser;
        _settings = options.Value;
        _logger = logger;
        _matchFetchService = matchFetchService;
        _coachRefreshService = coachRefreshService;
        _fileStore = fileStore;
        _logger.LogInformation("BatchController initialized — mode: {Mode}", _settings.DefaultMode);
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(BuildIndexModel());
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("file", "Please select a CSV file.");
            return View("Index", BuildIndexModel());
        }

        var tempPath = Path.GetTempFileName();
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var matches = await _csvParser.ParseMatchesAsync(tempPath);
            return PredictMatches(matches);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    /// <summary>« Actualiser les entraîneurs » : récupère les entraîneurs actuels des 4 championnats.</summary>
    [HttpPost]
    public async Task<IActionResult> RefreshCoaches(CancellationToken ct)
    {
        var model = BuildIndexModel();
        if (_coachRefreshService == null)
        {
            model.Error = "La mise à jour des entraîneurs n'est pas disponible.";
            return View("Index", model);
        }

        var codes = ChampionshipService.DefaultChampionships.Select(c => c.Code);
        model.CoachRefresh = await _coachRefreshService.RefreshAsync(codes, DateTime.Today, ct);
        _logger.LogInformation("Coach refresh: {Changed} changed, {Unchanged} unchanged, {Errors} errors",
            model.CoachRefresh.Changed.Count, model.CoachRefresh.Unchanged, model.CoachRefresh.Errors.Count);
        return View("Index", model);
    }

    /// <summary>
    /// « Récupérer et prédire » : récupère les matchs d'une période, les enregistre dans
    /// un CSV, puis prédit directement à partir de ce CSV.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> FetchMatches(string[] leagues, DateTime from, DateTime to, CancellationToken ct)
    {
        var model = BuildIndexModel();
        model.From = from;
        model.To = to;

        if (_matchFetchService == null || _fileStore == null)
        {
            model.Error = "La récupération des matchs n'est pas disponible.";
            return View("Index", model);
        }
        if (leagues == null || leagues.Length == 0)
        {
            model.Error = "Choisissez au moins un championnat.";
            return View("Index", model);
        }
        if (to < from)
        {
            model.Error = "La date de fin doit être après la date de début.";
            return View("Index", model);
        }
        if ((to - from).TotalDays > MaxPeriodDays)
        {
            model.Error = $"La période est limitée à {MaxPeriodDays} jours.";
            return View("Index", model);
        }

        var fetched = await _matchFetchService.FetchAsync(leagues, from, to, ct);
        if (fetched.FileName == null)
        {
            model.Error = fetched.Errors.Count > 0
                ? string.Join(" — ", fetched.Errors)
                : "Aucun match trouvé sur cette période.";
            return View("Index", model);
        }

        var warnings = fetched.Errors
            .Concat(UnknownTeamWarnings(fetched.UnknownTeams))
            .Concat(OddsWarnings(fetched))
            .ToList();
        return await PredictFromFileAsync(fetched.FileName, warnings);
    }

    /// <summary>« Prédire à nouveau » à partir d'un CSV déjà créé (ex. après mise à jour des entraîneurs).</summary>
    [HttpPost]
    public async Task<IActionResult> PredictFile(string fileName)
    {
        return await PredictFromFileAsync(fileName, Array.Empty<string>());
    }

    [HttpGet]
    public IActionResult DownloadFile(string fileName)
    {
        var path = _fileStore?.FindExisting(fileName);
        if (path == null)
            return NotFound();
        return PhysicalFile(path, "text/csv", fileName);
    }

    [HttpGet]
    public IActionResult Download()
    {
        var csv = TempData["BatchPredictionsCsv"] as string;
        if (string.IsNullOrEmpty(csv))
            return RedirectToAction("Index");

        var fileName = $"predictions_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    private async Task<IActionResult> PredictFromFileAsync(string fileName, IReadOnlyList<string> warnings)
    {
        var path = _fileStore?.FindExisting(fileName);
        if (path == null)
        {
            var model = BuildIndexModel();
            model.Error = "Fichier introuvable.";
            return View("Index", model);
        }

        var matches = await _csvParser.ParseMatchesAsync(path);
        return PredictMatches(matches, fileName, warnings);
    }

    private IActionResult PredictMatches(
        IReadOnlyList<Match> matches, string? sourceFileName = null, IReadOnlyList<string>? warnings = null)
    {
        // Prédiction batch enrichie (multiclass 1/X/2 + coachs à jour + bet).
        var items = _batchPredictionService.Predict(matches);

        var csv = new StringBuilder();
        csv.AppendLine(BatchResultFormatter.CsvHeader);
        var viewModels = new List<PredictionResultViewModel>();
        foreach (var it in items)
        {
            csv.AppendLine(BatchResultFormatter.ToCsv(new[] { it }).Trim());
            viewModels.Add(new PredictionResultViewModel
            {
                Date = it.Date,
                League = it.League,
                HomeTeam = it.HomeTeam,
                AwayTeam = it.AwayTeam,
                HomeCoach = it.HomeCoach,
                AwayCoach = it.AwayCoach,
                PredictedResult = it.PredictedResult,
                Probability1 = it.Probability1,
                ProbabilityX = it.ProbabilityX,
                Probability2 = it.Probability2,
                Confidence = it.Confidence,
                Comment = it.Comment,
                HomeWinProbability = it.Probability1,
                AwayWinProbability = it.Probability2,
                ModelUsed = "ML Model (Batch)",
                Mode = "Multiclass"
            });
        }

        TempData["BatchPredictionsCsv"] = csv.ToString();

        _logger.LogInformation("Batch processed: {Total} matches, {Predictions} predictions",
            matches.Count, items.Count);

        return View("Result", new BatchResultViewModel
        {
            TotalMatches = matches.Count,
            SuccessfulPredictions = items.Count,
            Predictions = viewModels,
            DownloadFileName = $"predictions_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            SourceFileName = sourceFileName,
            Warnings = warnings ?? Array.Empty<string>()
        });
    }

    private BatchIndexViewModel BuildIndexModel() => new()
    {
        Championships = ChampionshipService.DefaultChampionships,
        From = DateTime.Today,
        To = DateTime.Today.AddDays(7),
        Files = _fileStore?.List() ?? Array.Empty<MatchFileInfo>()
    };

    private static IEnumerable<string> OddsWarnings(MatchFetchResult fetched)
    {
        if (!fetched.OddsEnabled)
            yield break;
        foreach (var w in fetched.OddsWarnings)
            yield return w;
        if (fetched.OddsWarnings.Count == 0 && fetched.MatchesWithOdds < fetched.Matches.Count)
        {
            var names = fetched.MatchesWithoutOdds;
            var shown = string.Join(", ", names.Take(8)) + (names.Count > 8 ? $" … (+{names.Count - 8})" : "");
            yield return $"Cotes trouvées pour {fetched.MatchesWithOdds} match(s) sur {fetched.Matches.Count}. " +
                         (names.Count > 0
                             ? $"Prédits sans cotes (moins fiable) : {shown}."
                             : "Les autres sont prédits sans cotes (moins fiable).");
        }
    }

    private static IEnumerable<string> UnknownTeamWarnings(IReadOnlyList<string> unknown) =>
        unknown.Count == 0
            ? Array.Empty<string>()
            : new[] { $"Équipes non reconnues (prédiction moins fiable) : {string.Join(", ", unknown)}." };
}
