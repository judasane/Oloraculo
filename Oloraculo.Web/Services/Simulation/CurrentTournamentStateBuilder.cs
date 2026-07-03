using Microsoft.EntityFrameworkCore;
using Oloraculo.Web.DAL;
using Oloraculo.Web.Models;

namespace Oloraculo.Web.Services.Simulation;

public sealed class CurrentTournamentStateBuilder
{
    private readonly OloraculoDbContext _db;

    public CurrentTournamentStateBuilder(OloraculoDbContext db) => _db = db;

    public async Task<CurrentTournamentState> BuildAsync(CancellationToken ct = default)
    {
        var groups = await _db.Groups.AsNoTracking().OrderBy(g => g.Name).ToListAsync(ct);
        var fixtures = await _db.Fixtures.AsNoTracking().ToListAsync(ct);
        var knockout = await _db.KnockoutMatches.AsNoTracking().ToDictionaryAsync(m => m.MatchNumber, ct);
        return Build(groups, fixtures, knockout);
    }

    public static CurrentTournamentState Build(
        IReadOnlyList<Group> groups,
        IReadOnlyList<Fixture> fixtures,
        IReadOnlyDictionary<int, KnockoutMatch> knockout)
    {
        var teams = groups.SelectMany(g => g.TeamIds).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reached = new Dictionary<string, KnockoutStageEnum>(StringComparer.OrdinalIgnoreCase);
        var playedWinners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var playedLosers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var confirmedTeams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var playedStages = new List<KnockoutStageEnum>();

        foreach (var match in knockout.Values)
        {
            AddConfirmed(match.ConfirmedHomeTeamId);
            AddConfirmed(match.ConfirmedAwayTeamId);

            if (match.ConfirmedHomeTeamId is not null)
                MarkReached(match.ConfirmedHomeTeamId, match.Stage);
            if (match.ConfirmedAwayTeamId is not null)
                MarkReached(match.ConfirmedAwayTeamId, match.Stage);

            if (!match.IsPlayed || match.WinnerTeamId is null)
                continue;

            playedStages.Add(match.Stage);
            playedWinners.Add(match.WinnerTeamId);
            if (match.ConfirmedHomeTeamId is not null && match.ConfirmedAwayTeamId is not null)
            {
                var loser = string.Equals(match.WinnerTeamId, match.ConfirmedHomeTeamId, StringComparison.OrdinalIgnoreCase)
                    ? match.ConfirmedAwayTeamId
                    : match.ConfirmedHomeTeamId;
                playedLosers.Add(loser);
            }
            MarkReached(match.WinnerTeamId, NextStage(match.Stage) ?? match.Stage);
        }

        var knockoutStarted = knockout.Values.Any(m =>
            m.IsPlayed ||
            m.ConfirmedHomeTeamId is not null ||
            m.ConfirmedAwayTeamId is not null);

        var groupStageComplete = fixtures.Count > 0 && fixtures.All(f => f.IsPlayed);
        var eliminated = new HashSet<string>(playedLosers, StringComparer.OrdinalIgnoreCase);
        var alive = new HashSet<string>(confirmedTeams, StringComparer.OrdinalIgnoreCase);
        alive.ExceptWith(eliminated);

        if (knockoutStarted && groupStageComplete)
        {
            foreach (var team in teams)
                if (!reached.ContainsKey(team))
                    eliminated.Add(team);
        }

        return new CurrentTournamentState
        {
            Groups = groups,
            Fixtures = fixtures,
            KnockoutMatches = knockout,
            AliveTeamIds = alive,
            EliminatedTeamIds = eliminated,
            ReachedStageByTeam = reached,
            CurrentStage = playedStages.Count == 0 ? null : playedStages.Max(),
            IsGroupStageComplete = groupStageComplete,
            IsKnockoutStarted = knockoutStarted
        };

        void AddConfirmed(string? teamId)
        {
            if (teamId is not null)
                confirmedTeams.Add(teamId);
        }

        void MarkReached(string teamId, KnockoutStageEnum stage)
        {
            if (!reached.TryGetValue(teamId, out var current) || stage > current)
                reached[teamId] = stage;
        }
    }

    public static KnockoutStageEnum? NextStage(KnockoutStageEnum stage) => stage switch
    {
        KnockoutStageEnum.RoundOf32 => KnockoutStageEnum.RoundOf16,
        KnockoutStageEnum.RoundOf16 => KnockoutStageEnum.QuarterFinal,
        KnockoutStageEnum.QuarterFinal => KnockoutStageEnum.SemiFinal,
        KnockoutStageEnum.SemiFinal => KnockoutStageEnum.Final,
        KnockoutStageEnum.Final => null,
        KnockoutStageEnum.ThirdPlace => null,
        _ => null
    };
}
