using FootballPrediction.Application.Interfaces;

namespace FootballPrediction.Application.Services;

public sealed record CoachChange(string Team, string? PreviousCoach, string NewCoach);

public sealed class CoachRefreshResult
{
    public IReadOnlyList<CoachChange> Changed { get; init; } = Array.Empty<CoachChange>();
    public int Unchanged { get; init; }
    public IReadOnlyList<string> UnknownTeams { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Met à jour les entraîneurs des championnats depuis la source externe.
/// N'enregistre que les changements (pas de doublon dans l'historique).
/// </summary>
public sealed class CoachRefreshService
{
    private readonly IFootballDataClient _client;
    private readonly ICoachRepository _repository;
    private readonly CoachService _coachService;

    public CoachRefreshService(IFootballDataClient client, ICoachRepository repository, CoachService coachService)
    {
        _client = client;
        _repository = repository;
        _coachService = coachService;
    }

    public async Task<CoachRefreshResult> RefreshAsync(
        IEnumerable<string> leagueCodes, DateTime asOf, CancellationToken ct = default)
    {
        var changed = new List<CoachChange>();
        var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var unchanged = 0;

        foreach (var code in leagueCodes.Where(c => !string.IsNullOrWhiteSpace(c))
                     .Select(c => c.Trim().ToUpperInvariant()).Distinct())
        {
            try
            {
                var league = ChampionshipService.DefaultChampionships
                    .FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))?.Name;

                foreach (var tc in await _client.GetTeamCoachesAsync(code, ct))
                {
                    if (string.IsNullOrWhiteSpace(tc.Coach))
                        continue;

                    var team = TeamNameMapper.Map(tc.Team);
                    if (!team.Known) unknown.Add(team.Name);

                    var current = _repository.GetCoach(team.Name, asOf);
                    if (string.Equals(current?.Trim(), tc.Coach.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        unchanged++;
                        continue;
                    }

                    _coachService.UpdateCoach(team.Name, tc.Coach, asOf, league);
                    changed.Add(new CoachChange(team.Name, current, tc.Coach.Trim()));
                }
            }
            catch (FootballDataException ex)
            {
                errors.Add($"{code} : {ex.Message}");
            }
        }

        return new CoachRefreshResult
        {
            Changed = changed,
            Unchanged = unchanged,
            UnknownTeams = unknown.ToList(),
            Errors = errors
        };
    }
}
