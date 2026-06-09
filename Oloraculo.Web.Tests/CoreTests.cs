using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Oloraculo.Web;
using Oloraculo.Web.DAL;
using Oloraculo.Web.Helpers;
using Oloraculo.Web.Models;
using Oloraculo.Web.Models.CsvModels;
using Oloraculo.Web.Predictors;
using Oloraculo.Web.Probability;
using Oloraculo.Web.Services;
using Oloraculo.Web.Services.Simulation;
using System.Globalization;
using System.Net;

namespace Oloraculo.Web.Tests;

public class CoreTests
{
    [Fact]
    public void OutcomeProbabilities_NormalizesAndUsesOutcomeLabels()
    {
        var p = new OutcomeProbabilities(2, 1, 1).Normalize();

        Assert.True(p.IsValid);
        Assert.Equal(0.5, p.HomeWin, 3);
        Assert.Equal("Home", p.TopPick);
    }

    [Theory]
    [InlineData("Korea Republic", "south-korea")]
    [InlineData("Türkiye", "turkey")]
    [InlineData("USA", "united-states")]
    public void TeamNameNormalizer_HandlesAliases(string input, string expected)
    {
        Assert.Equal(expected, TeamNameNormalizer.ToId(input));
    }

    [Fact]
    public void OutcomeFromExpectation_TreatsEqualMagnitudeGapsSymmetrically()
    {
        var strongerHome = ProbabilityHelper.OutcomeFromExpectation(.78, 400);
        var strongerAway = ProbabilityHelper.OutcomeFromExpectation(.22, -400);

        Assert.Equal(strongerHome.Draw, strongerAway.Draw, 6);
    }

    [Fact]
    public void PoissonScoreline_ProducesARealProbabilityGrid()
    {
        var dist = ProbabilityHelper.PoissonScoreline(2.2, .7);
        var sum = 0.0;
        for (var h = 0; h <= dist.MaxGoals; h++)
            for (var a = 0; a <= dist.MaxGoals; a++)
                sum += dist.Probability(h, a);

        Assert.Equal(1.0, sum, 6);
        Assert.True(dist.ToOutcome().HomeWin > dist.ToOutcome().AwayWin);
        Assert.NotEqual((0, 0), dist.MostLikelyScoreline());
    }

    [Fact]
    public void GoalModel_ProducesUsableScorelineWhenTeamsHaveEnoughHistory()
    {
        var model = new GoalModel(
        [
            Result("a", "b", 2, 0),
            Result("a", "b", 1, 0),
            Result("b", "a", 1, 2)
        ]);

        var prediction = model.Predict(TestContext());

        Assert.False(prediction.Degraded);
        Assert.NotNull(prediction.Scoreline);
        Assert.True(prediction.ExpectedHomeGoals > 0.1);
        Assert.True(prediction.Outcome.IsValid);
    }

    [Fact]
    public void ContextModel_DoesNotClaimLineupsOrOddsWereUsedWithoutConversionLogic()
    {
        var goal = new GoalModel(
        [
            Result("a", "b", 2, 0),
            Result("a", "b", 1, 0),
            Result("b", "a", 1, 2)
        ]);
        var context = TestContext(fixtureContext: new FixtureContext
        {
            FixtureId = "test",
            HasLineups = true,
            HasOdds = true
        });

        var prediction = new GoalPlusRecentContextModel(goal).Predict(context);

        Assert.DoesNotContain(nameof(FeaturesEnum.Lineups), prediction.FeaturesUsed);
        Assert.DoesNotContain(nameof(FeaturesEnum.Odds), prediction.FeaturesUsed);
        Assert.Contains("lineup impact model", prediction.FeaturesMissing);
        Assert.Contains("odds calibration", prediction.FeaturesMissing);
        Assert.True(prediction.Degraded);
    }

    [Fact]
    public void ContextModel_BecomesUsableWhenAvailabilityActuallyAdjustsGoals()
    {
        var goal = new GoalModel(
        [
            Result("a", "b", 2, 0),
            Result("a", "b", 1, 0),
            Result("b", "a", 1, 2)
        ]);
        var context = TestContext(fixtureContext: new FixtureContext
        {
            FixtureId = "test",
            UnavailableHomePlayers = 2
        });

        var prediction = new GoalPlusRecentContextModel(goal).Predict(context);

        Assert.False(prediction.Degraded);
        Assert.Contains(nameof(FeaturesEnum.PlayerAvailability), prediction.FeaturesUsed);
    }

    [Fact]
    public void FinalSelector_ChoosesHighestUsableRungWithoutAveraging()
    {
        var form = Prediction(3, "Recent Form", .05, .05, .90);
        var goal = Prediction(4, "Goal", .90, .05, .05, scoreline: ProbabilityHelper.PoissonScoreline(3.0, .4));
        var context = Prediction(5, "Context", .10, .80, .10, degraded: true, missing: ["availability"]);

        var final = FinalPredictionSelector.Select([form, goal, context]);

        Assert.Equal("Final Oracle", final.PredictorName);
        Assert.Equal(4, final.PredictorPriority);
        Assert.Equal(goal.Outcome, final.Outcome);
        Assert.NotEqual(.475, final.Outcome.HomeWin, 3);
    }

    [Fact]
    public void FinalSelector_AppliesLightRankingBiasWhenEloAndFifaAgreeAgainstSelected()
    {
        var fifa = Prediction(1, "FIFA ranking", .15, .20, .65, sources: [SourceMetadata.FifaRankings]);
        var elo = Prediction(2, "Elo", .10, .20, .70, sources: [SourceMetadata.EloRatings]);
        var goalScoreline = ProbabilityHelper.PoissonScoreline(1.4, 1.1);
        var goal = Prediction(4, "Goal", .45, .35, .20, scoreline: goalScoreline);

        var final = FinalPredictionSelector.Select([fifa, elo, goal]);

        Assert.Equal("Final Oracle", final.PredictorName);
        Assert.Equal(4, final.PredictorPriority);
        Assert.Equal(.40125, final.Outcome.HomeWin, 5);
        Assert.Equal(.3275, final.Outcome.Draw, 5);
        Assert.Equal(.27125, final.Outcome.AwayWin, 5);
        Assert.Same(goalScoreline, final.Scoreline);
        Assert.Contains(final.Drivers, d => d.Contains("Elo/FIFA calibration"));
        Assert.Contains("Elo/FIFA calibration", final.Explanation);
        Assert.Contains(SourceMetadata.FifaRankings, final.Sources);
        Assert.Contains(SourceMetadata.EloRatings, final.Sources);
    }

    [Fact]
    public void FinalSelector_DoesNotApplyRankingBiasWhenRankingModelsDisagree()
    {
        var fifa = Prediction(1, "FIFA ranking", .65, .20, .15, sources: [SourceMetadata.FifaRankings]);
        var elo = Prediction(2, "Elo", .10, .20, .70, sources: [SourceMetadata.EloRatings]);
        var goal = Prediction(4, "Goal", .45, .35, .20);

        var final = FinalPredictionSelector.Select([fifa, elo, goal]);

        Assert.Equal(goal.Outcome, final.Outcome);
        Assert.DoesNotContain(final.Drivers, d => d.Contains("Elo/FIFA calibration"));
        Assert.DoesNotContain(SourceMetadata.FifaRankings, final.Sources);
        Assert.DoesNotContain(SourceMetadata.EloRatings, final.Sources);
    }

    [Fact]
    public void FinalSelector_DoesNotApplyRankingBiasWhenRankingModelIsDegraded()
    {
        var fifa = Prediction(1, "FIFA ranking", .15, .20, .65, degraded: true, sources: [SourceMetadata.FifaRankings]);
        var elo = Prediction(2, "Elo", .10, .20, .70, sources: [SourceMetadata.EloRatings]);
        var goal = Prediction(4, "Goal", .45, .35, .20);

        var final = FinalPredictionSelector.Select([fifa, elo, goal]);

        Assert.Equal(goal.Outcome, final.Outcome);
        Assert.DoesNotContain(final.Drivers, d => d.Contains("Elo/FIFA calibration"));
        Assert.DoesNotContain(SourceMetadata.FifaRankings, final.Sources);
        Assert.DoesNotContain(SourceMetadata.EloRatings, final.Sources);
    }

    [Fact]
    public void RankingRefresh_ParsesFifaLuaRows()
    {
        var rows = RankingRefreshService.ParseFifaRankings(SampleFifaRaw());

        Assert.Equal(2, rows.Count);
        Assert.Equal("France", rows[0].Team);
        Assert.Equal("1877.32", rows[0].Points);
        Assert.Equal("2026-04-01", rows[0].RankingDate);
    }

    [Fact]
    public void RankingRefresh_ParsesEloHtmlRowsAndCleansImageText()
    {
        var date = new DateOnly(2026, 6, 5);
        var rows = RankingRefreshService.ParseEloRankings(SampleEloHtml(), date, "https://example.test/elo");

        Assert.Equal(2, rows.Count);
        Assert.Equal("Spain", rows[0].Team);
        Assert.Equal("2155", rows[0].Elo);
        Assert.Equal("2026-06-05", rows[0].RatingDate);
    }

    [Fact]
    public async Task RankingRefresh_WalksBackToLatestAvailableEloDateAndWritesParseableCsvs()
    {
        var root = NewTempRoot();
        try
        {
            var options = Options.Create(new OloraculoConfig
            {
                FifaRankingsRawUrl = "https://example.test/fifa",
                EloRankingsBaseUrl = "https://example.test/elo",
                EloRefreshMaxLookbackDays = 3
            });
            var handler = new FakeHttpMessageHandler(new Dictionary<string, string>
            {
                ["https://example.test/fifa"] = SampleFifaRaw(),
                ["https://example.test/elo?day=09&month=06&year=2026"] = "no rankings today",
                ["https://example.test/elo?day=08&month=06&year=2026"] = "still no rankings",
                ["https://example.test/elo?day=07&month=06&year=2026"] = SampleEloHtml()
            });
            var service = new RankingRefreshService(new HttpClient(handler), new TestEnvironment(root), options);

            var report = await service.RefreshAsync(new DateOnly(2026, 6, 9));

            Assert.True(report.AnyFileUpdated);
            Assert.Equal(new DateOnly(2026, 6, 7), report.EloRatingDate);
            Assert.Equal(2, CsvParsingHelper.ReadCsv<FifaCsvRow>(Path.Combine(root, "Data", OloraculoDataFiles.FifaRankingsCsv)).Count);
            Assert.Equal(2, CsvParsingHelper.ReadCsv<EloCsvRow>(Path.Combine(root, "Data", OloraculoDataFiles.EloCsv)).Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RankingRefresh_DoesNotOverwriteExistingCsvsWhenSourcesCannotParse()
    {
        var root = NewTempRoot();
        try
        {
            var data = Path.Combine(root, "Data");
            Directory.CreateDirectory(data);
            var fifaPath = Path.Combine(data, OloraculoDataFiles.FifaRankingsCsv);
            var eloPath = Path.Combine(data, OloraculoDataFiles.EloCsv);
            await File.WriteAllTextAsync(fifaPath, "existing fifa");
            await File.WriteAllTextAsync(eloPath, "existing elo");

            var options = Options.Create(new OloraculoConfig
            {
                FifaRankingsRawUrl = "https://example.test/fifa",
                EloRankingsBaseUrl = "https://example.test/elo",
                EloRefreshMaxLookbackDays = 0
            });
            var handler = new FakeHttpMessageHandler(new Dictionary<string, string>
            {
                ["https://example.test/fifa"] = "not lua",
                ["https://example.test/elo?day=09&month=06&year=2026"] = "not elo"
            });
            var service = new RankingRefreshService(new HttpClient(handler), new TestEnvironment(root), options);

            var report = await service.RefreshAsync(new DateOnly(2026, 6, 9));

            Assert.False(report.AnyFileUpdated);
            Assert.Equal("existing fifa", await File.ReadAllTextAsync(fifaPath));
            Assert.Equal("existing elo", await File.ReadAllTextAsync(eloPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CsvImport_CreatesTeamsGroupsFixturesRatingsAndResults()
    {
        await using var db = await NewDb();
        var importer = new CsvImportService(db, new TestEnvironment(WebProjectRoot()));

        var report = await importer.ImportAllAsync();

        Assert.True(report.Teams >= 48);
        Assert.Equal(12, report.Groups);
        Assert.Equal(72, report.Fixtures);
        Assert.True(report.Ratings > 0);
        Assert.True(report.Results > 0);
        Assert.Equal(ExpectedUniqueHistoricalResultIds(), report.Results);
        Assert.DoesNotContain(await db.Fixtures.ToListAsync(), f => string.IsNullOrWhiteSpace(f.Group));
    }

    [Fact]
    public async Task Evaluation_StoresFixtureLevelKnownResult()
    {
        await using var db = await NewDb();
        var fixture = new Fixture { Id = "f1", Group = "A", HomeTeamId = "a", AwayTeamId = "b" };
        db.Teams.AddRange(new Team { Id = "a", Name = "A" }, new Team { Id = "b", Name = "B" });
        db.Fixtures.Add(fixture);
        db.Snapshots.Add(new PredictionSnapshot
        {
            Kind = "match",
            FixtureId = "f1",
            ModelName = "Final Oracle",
            InputSummaryHash = "hash",
            PayloadJson = "{}",
            Explanation = "test",
            HomeWin = .6,
            Draw = .2,
            AwayWin = .2
        });
        await db.SaveChangesAsync();

        var count = await new EvaluationService(db).EvaluateLatestSnapshotAsync(fixture, 2, 1);

        Assert.Equal(1, count);
        Assert.True(fixture.IsPlayed);
        Assert.Equal(2, fixture.HomeGoals);
        Assert.Equal(1, fixture.AwayGoals);
    }

    [Fact]
    public async Task SnapshotService_SavesTournamentSnapshotAgainstLegacyNonNullProbabilityColumns()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "Snapshots" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Snapshots" PRIMARY KEY AUTOINCREMENT,
                    "Kind" TEXT NOT NULL,
                    "FixtureId" TEXT NULL,
                    "ModelName" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "InputSummaryHash" TEXT NOT NULL,
                    "PayloadJson" TEXT NOT NULL,
                    "Explanation" TEXT NOT NULL,
                    "HomeWin" REAL NOT NULL,
                    "Draw" REAL NOT NULL,
                    "AwayWin" REAL NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<OloraculoDbContext>().UseSqlite(connection).Options;
        await using var db = new OloraculoDbContext(options);

        var snapshot = await new SnapshotService(db).SaveTournamentAsync(new TournamentProjection
        {
            ModelName = "Final",
            InputSummaryHash = "hash",
            Simulations = 1,
            Teams = []
        });

        Assert.Equal("tournament", snapshot.Kind);
        Assert.Equal(0, snapshot.AwayWin);
    }

    [Fact]
    public async Task Simulation_IsRepeatableWithSameSeed()
    {
        await using var db = await ImportedDb();
        var service = Simulation(db, simulations: 3, seed: 42);

        var one = await service.RunAsync(saveSnapshot: false);
        var two = await service.RunAsync(saveSnapshot: false);

        Assert.Equal(one.Teams.Select(t => t.WinTournament), two.Teams.Select(t => t.WinTournament));
        Assert.Equal(1.0, one.Teams.Sum(t => t.WinTournament), 6);
    }

    [Theory]
    [InlineData("argentina", "france")]
    [InlineData("france", "argentina")]
    [InlineData("mexico", "canada")]
    public async Task SimulationPredictionContext_MatchesPredictionServiceForPairs(string homeId, string awayId)
    {
        await using var db = await ImportedDb();
        var options = SimulationOptions(simulations: 1, seed: 42);
        var prediction = new PredictionService(db, options);
        var simulationPrediction = await SimulationPredictionContext.CreateAsync(db, options.Value);

        var expected = await prediction.PredictPairAsync(homeId, awayId);
        var actual = await simulationPrediction.PredictPairAsync(homeId, awayId);

        AssertPredictionResultEqual(expected, actual);
    }

    [Fact]
    public async Task Simulation_WithFixedSeedKeepsDeterministicTournamentOutput()
    {
        await using var db = await ImportedDb();
        var service = Simulation(db, simulations: 2, seed: 2026);

        var one = await service.RunAsync(saveSnapshot: false);
        var two = await service.RunAsync(saveSnapshot: false);

        Assert.Equal(2, one.Simulations);
        Assert.Equal(1.0, one.Teams.Sum(t => t.WinTournament), 6);
        Assert.Equal(one.Teams.Select(ProjectionKey), two.Teams.Select(ProjectionKey));
    }

    [Fact]
    public async Task Simulation_UsesKnownGroupFixtureScores()
    {
        await using var db = await ImportedDb();
        var mexicoFixtures = await db.Fixtures
            .Where(f => f.Group == "A" && (f.HomeTeamId == "mexico" || f.AwayTeamId == "mexico"))
            .ToListAsync();

        foreach (var fixture in mexicoFixtures)
        {
            fixture.IsPlayed = true;
            fixture.HomeGoals = fixture.HomeTeamId == "mexico" ? 10 : 0;
            fixture.AwayGoals = fixture.AwayTeamId == "mexico" ? 10 : 0;
        }
        await db.SaveChangesAsync();

        var projection = await Simulation(db, simulations: 5, seed: 7).RunAsync(saveSnapshot: false);
        var mexico = projection.Teams.Single(t => t.TeamId == "mexico");

        Assert.Equal(1.0, mexico.WinGroup, 6);
        Assert.Equal(1.0, mexico.Qualify, 6);
    }

    private static SimulationService Simulation(OloraculoDbContext db, int simulations, int seed)
    {
        var options = SimulationOptions(simulations, seed);
        var prediction = new PredictionService(db, options);
        var snapshots = new SnapshotService(db);
        return new SimulationService(db, prediction, snapshots, options);
    }

    private static IOptions<OloraculoConfig> SimulationOptions(int simulations, int seed) =>
        Options.Create(new OloraculoConfig
        {
            GoalModelYearsWindow = 3,
            RecentResultCount = 8,
            SimulationCount = simulations,
            SimulationSeed = seed
        });

    private static async Task<OloraculoDbContext> ImportedDb()
    {
        var db = await NewDb();
        await new CsvImportService(db, new TestEnvironment(WebProjectRoot())).ImportAllAsync();
        return db;
    }

    private static async Task<OloraculoDbContext> NewDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<OloraculoDbContext>().UseSqlite(connection).Options;
        var db = new OloraculoDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static MatchContext TestContext(string homeId = "a", string awayId = "b", FixtureContext? fixtureContext = null) => new()
    {
        Fixture = new Fixture { Id = "test", HomeTeamId = homeId, AwayTeamId = awayId, NeutralVenue = true },
        HomeTeam = new Team { Id = homeId, Name = homeId.ToUpperInvariant() },
        AwayTeam = new Team { Id = awayId, Name = awayId.ToUpperInvariant() },
        HomeElo = new Rating { TeamId = homeId, Type = RatingTypeEnum.Elo, Value = 1800, Source = "test" },
        AwayElo = new Rating { TeamId = awayId, Type = RatingTypeEnum.Elo, Value = 1700, Source = "test" },
        HomeRecentMatchHistory = [],
        AwayRecentMatchHistory = [],
        FixtureContext = fixtureContext
    };

    private static MatchResult Result(string home, string away, int homeGoals, int awayGoals) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        HomeTeamId = home,
        AwayTeamId = away,
        HomeGoals = homeGoals,
        AwayGoals = awayGoals,
        Date = DateTimeOffset.UtcNow.AddDays(-Random.Shared.Next(1, 30)),
        Tournament = "test",
        Neutral = true,
        Source = "test"
    };

    private static MatchPrediction Prediction(
        int priority,
        string name,
        double home,
        double draw,
        double away,
        bool degraded = false,
        IReadOnlyList<string>? missing = null,
        ScorelineDistribution? scoreline = null,
        IReadOnlyList<SourceMetadata>? sources = null) => new()
    {
        PredictorPriority = priority,
        PredictorName = name,
        FixtureId = "f",
        HomeTeamId = "a",
        AwayTeamId = "b",
        Outcome = new OutcomeProbabilities(home, draw, away).Normalize(),
        Scoreline = scoreline,
        Explanation = name,
        FeaturesMissing = missing ?? [],
        Sources = sources ?? [],
        Degraded = degraded
    };

    private static void AssertPredictionResultEqual(MatchPredictionResult expected, MatchPredictionResult actual)
    {
        Assert.Equal(expected.Fixture.Id, actual.Fixture.Id);
        Assert.Equal(expected.Fixture.HomeTeamId, actual.Fixture.HomeTeamId);
        Assert.Equal(expected.Fixture.AwayTeamId, actual.Fixture.AwayTeamId);
        Assert.Equal(expected.HomeTeamName, actual.HomeTeamName);
        Assert.Equal(expected.AwayTeamName, actual.AwayTeamName);
        Assert.Equal(expected.Predictions.Count, actual.Predictions.Count);

        for (var i = 0; i < expected.Predictions.Count; i++)
            AssertPredictionEqual(expected.Predictions[i], actual.Predictions[i]);

        AssertPredictionEqual(expected.BestPrediction, actual.BestPrediction);
    }

    private static void AssertPredictionEqual(MatchPrediction expected, MatchPrediction actual)
    {
        Assert.Equal(expected.PredictorName, actual.PredictorName);
        Assert.Equal(expected.PredictorPriority, actual.PredictorPriority);
        Assert.Equal(expected.FixtureId, actual.FixtureId);
        Assert.Equal(expected.HomeTeamId, actual.HomeTeamId);
        Assert.Equal(expected.AwayTeamId, actual.AwayTeamId);
        Assert.Equal(expected.Outcome.HomeWin, actual.Outcome.HomeWin);
        Assert.Equal(expected.Outcome.Draw, actual.Outcome.Draw);
        Assert.Equal(expected.Outcome.AwayWin, actual.Outcome.AwayWin);
        Assert.Equal(expected.ExpectedHomeGoals, actual.ExpectedHomeGoals);
        Assert.Equal(expected.ExpectedAwayGoals, actual.ExpectedAwayGoals);
        Assert.Equal(expected.MostLikelyScore, actual.MostLikelyScore);
        Assert.Equal(expected.Degraded, actual.Degraded);
        Assert.Equal(expected.FeaturesMissing, actual.FeaturesMissing);
        AssertScorelineEqual(expected.Scoreline, actual.Scoreline);
    }

    private static void AssertScorelineEqual(ScorelineDistribution? expected, ScorelineDistribution? actual)
    {
        Assert.Equal(expected is null, actual is null);
        if (expected is null || actual is null)
            return;

        Assert.Equal(expected.MaxGoals, actual.MaxGoals);
        for (var home = 0; home <= expected.MaxGoals; home++)
            for (var away = 0; away <= expected.MaxGoals; away++)
                Assert.Equal(expected.Probability(home, away), actual.Probability(home, away));
    }

    private static object ProjectionKey(TeamTournamentProbability team) => new
    {
        team.TeamId,
        team.Group,
        team.WinGroup,
        team.Qualify,
        team.ReachRoundOf16,
        team.ReachQuarterFinal,
        team.ReachSemiFinal,
        team.ReachFinal,
        team.WinTournament,
        team.ExpectedGroupPoints
    };

    private static string WebProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Oloraculo.Web"));

    private static string NewTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "OloraculoTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string SampleFifaRaw() =>
        """
        local data = {}
        data.updated  = { day = 1, month = 'April', year =2026 }
        data.rankings = {
            { "France", 1, 2, 1877.32 },
            { "Spain", 2, -1, 1876.40 },
        }
        """;

    private static string SampleEloHtml() =>
        """
        <html><body>
        <h2>World football Elo ratings as on June 5th, 2026</h2>
        <p>1 . Image: Spain Spain 2155 2 . Argentina 2113</p>
        <p>About International-football.net</p>
        </body></html>
        """;

    private static int ExpectedUniqueHistoricalResultIds()
    {
        var rows = CsvParsingHelper.ReadCsv<HistoricalResultCsvRow>(Path.Combine(WebProjectRoot(), "Data", OloraculoDataFiles.HistoricalResultsCsv));
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (!DateTimeOffset.TryParse(row.Date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ||
                !int.TryParse(row.HomeScore, NumberStyles.Integer, CultureInfo.InvariantCulture, out var homeScore) ||
                !int.TryParse(row.AwayScore, NumberStyles.Integer, CultureInfo.InvariantCulture, out var awayScore))
            {
                continue;
            }

            var homeId = TeamNameNormalizer.ToId(row.HomeTeam);
            var awayId = TeamNameNormalizer.ToId(row.AwayTeam);
            ids.Add(CryptoUtil.GetSha256($"{homeId}-{awayId}-{date:O}-{row.Tournament}-{homeScore}-{awayScore}"));
        }

        return ids.Count;
    }

    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Oloraculo.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeHttpMessageHandler(IReadOnlyDictionary<string, string> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? "";
            if (!responses.TryGetValue(uri, out var content))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }
}
