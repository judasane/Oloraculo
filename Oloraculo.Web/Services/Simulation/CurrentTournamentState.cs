using Oloraculo.Web.Models;

namespace Oloraculo.Web.Services.Simulation;

public sealed class CurrentTournamentState
{
    public required IReadOnlyList<Group> Groups { get; init; }
    public required IReadOnlyList<Fixture> Fixtures { get; init; }
    public required IReadOnlyDictionary<int, KnockoutMatch> KnockoutMatches { get; init; }
    public required IReadOnlySet<string> AliveTeamIds { get; init; }
    public required IReadOnlySet<string> EliminatedTeamIds { get; init; }
    public required IReadOnlyDictionary<string, KnockoutStageEnum> ReachedStageByTeam { get; init; }
    public KnockoutStageEnum? CurrentStage { get; init; }
    public bool IsGroupStageComplete { get; init; }
    public bool IsKnockoutStarted { get; init; }
}
