using Microsoft.EntityFrameworkCore;
using Oloraculo.Web.DAL;
using Oloraculo.Web.Models;
using Oloraculo.Web.Predictors;

namespace Oloraculo.Web.Services.Simulation
{
    public sealed class SimulationPredictionContext
    {
        private readonly IReadOnlyList<Team> _teams;
        private readonly IReadOnlyList<Rating> _ratings;
        private readonly IReadOnlyList<MatchResult> _results;
        private readonly IReadOnlyList<FixtureContext> _fixtureContexts;
        private readonly IReadOnlyList<IPredictor> _predictors;
        private readonly int _recentResultCount;
        private readonly Dictionary<(string TeamId, RatingTypeEnum Type), Rating?> _ratingCache = [];
        private readonly Dictionary<string, IReadOnlyList<MatchResult>> _recentResultsCache = new(StringComparer.Ordinal);

        private SimulationPredictionContext(
            IReadOnlyList<Team> teams,
            IReadOnlyList<Rating> ratings,
            IReadOnlyList<MatchResult> results,
            IReadOnlyList<FixtureContext> fixtureContexts,
            OloraculoConfig config)
        {
            _teams = teams;
            _ratings = ratings;
            _results = results;
            _fixtureContexts = fixtureContexts;
            _recentResultCount = config.RecentResultCount;

            var goal = new GoalModel(results, config.GoalModelYearsWindow);
            _predictors =
            [
                new NullModel(),
                new FifaRankingModel(),
                new EloModel(),
                new RecentFormModel(),
                goal,
                new GoalPlusRecentContextModel(goal)
            ];
        }

        public static async Task<SimulationPredictionContext> CreateAsync(
            OloraculoDbContext db,
            OloraculoConfig config,
            CancellationToken ct = default)
        {
            var teams = await db.Teams.AsNoTracking().ToListAsync(ct);
            var ratings = await db.Ratings.AsNoTracking().ToListAsync(ct);
            var results = await db.Results.AsNoTracking().ToListAsync(ct);
            var fixtureContexts = await db.FixtureContexts.AsNoTracking().ToListAsync(ct);

            return new SimulationPredictionContext(teams, ratings, results, fixtureContexts, config);
        }

        public Task<MatchPredictionResult> PredictPairAsync(string homeId, string awayId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var fixture = new Fixture { Id = $"pair:{homeId}:{awayId}", HomeTeamId = homeId, AwayTeamId = awayId, NeutralVenue = true };
            return Task.FromResult(Predict(fixture));
        }

        private MatchPredictionResult Predict(Fixture fixture)
        {
            var context = BuildContext(fixture);
            var ladder = _predictors.Select(p => p.Predict(context)).ToList();

            return new MatchPredictionResult
            {
                Fixture = fixture,
                HomeTeamName = context.HomeTeam.Name,
                AwayTeamName = context.AwayTeam.Name,
                Predictions = ladder,
                BestPrediction = FinalPredictionSelector.Select(ladder)
            };
        }

        private MatchContext BuildContext(Fixture fixture)
        {
            var home = _teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId) ?? new Team { Id = fixture.HomeTeamId, Name = fixture.HomeTeamId };
            var away = _teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId) ?? new Team { Id = fixture.AwayTeamId, Name = fixture.AwayTeamId };

            return new MatchContext
            {
                Fixture = fixture,
                HomeTeam = home,
                AwayTeam = away,
                HomeElo = LatestRating(fixture.HomeTeamId, RatingTypeEnum.Elo),
                AwayElo = LatestRating(fixture.AwayTeamId, RatingTypeEnum.Elo),
                HomeFifaRank = LatestRating(fixture.HomeTeamId, RatingTypeEnum.Fifa),
                AwayFifaRank = LatestRating(fixture.AwayTeamId, RatingTypeEnum.Fifa),
                HomeRecentMatchHistory = RecentResults(fixture.HomeTeamId),
                AwayRecentMatchHistory = RecentResults(fixture.AwayTeamId),
                FixtureContext = _fixtureContexts.FirstOrDefault(c => c.FixtureId == fixture.Id)
            };
        }

        private Rating? LatestRating(string teamId, RatingTypeEnum type)
        {
            var key = (teamId, type);
            if (_ratingCache.TryGetValue(key, out var cached))
                return cached;

            cached = _ratings
                .Where(r => r.TeamId == teamId && r.Type == type)
                .ToList()
                .OrderByDescending(r => r.AsOf)
                .FirstOrDefault();
            _ratingCache[key] = cached;
            return cached;
        }

        private IReadOnlyList<MatchResult> RecentResults(string teamId)
        {
            if (_recentResultsCache.TryGetValue(teamId, out var cached))
                return cached;

            cached = _results
                .Where(r => r.HomeTeamId == teamId || r.AwayTeamId == teamId)
                .ToList()
                .OrderByDescending(r => r.Date)
                .Take(_recentResultCount)
                .ToList();
            _recentResultsCache[teamId] = cached;
            return cached;
        }
    }
}
