using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Oloraculo.Web;
using Oloraculo.Web.DAL;
using Oloraculo.Web.Helpers;
using Oloraculo.Web.Models;
using Oloraculo.Web.Models.ApiFootballModels;
using Oloraculo.Web.Models.CsvModels;
using Oloraculo.Web.Predictors;
using Oloraculo.Web.Probability;
using Oloraculo.Web.Services;
using Oloraculo.Web.Services.Simulation;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Oloraculo.Web.Tests;

public class SimulationServiceTests : TestFixtures
{
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

        var mexicoKnockouts = await db.KnockoutMatches
            .Where(m => m.ConfirmedHomeTeamId == "mexico" || m.ConfirmedAwayTeamId == "mexico")
            .ToListAsync();
        db.KnockoutMatches.RemoveRange(mexicoKnockouts);

        await db.SaveChangesAsync();

        var projection = await Simulation(db, simulations: 5, seed: 7).RunAsync(saveSnapshot: false);
        var mexico = projection.Teams.Single(t => t.TeamId == "mexico");

        Assert.Equal(1.0, mexico.WinGroup, 6);
        Assert.Equal(1.0, mexico.Qualify, 6);
    }

    [Fact]
    public async Task Simulation_UsesCurrentKnockoutStateWithoutReplayingGroups()
    {
        await using var db = await ImportedDb();
        await SeedRoundOf32Async(db);
        UpsertKnockout(db, 83, KnockoutStageEnum.RoundOf32, "portugal", "croatia", isPlayed: true, winner: "portugal", homeGoals: 2, awayGoals: 1);
        UpsertKnockout(db, 84, KnockoutStageEnum.RoundOf32, "spain", "belgium", isPlayed: true, winner: "spain", homeGoals: 2, awayGoals: 0);
        UpsertKnockout(db, 93, KnockoutStageEnum.RoundOf16, "portugal", "spain");
        await db.SaveChangesAsync();

        var projection = await Simulation(db, simulations: 50, seed: 2026).RunAsync(saveSnapshot: false);
        var portugal = projection.Teams.Single(t => t.TeamId == "portugal");
        var croatia = projection.Teams.Single(t => t.TeamId == "croatia");

        Assert.Equal(1.0, portugal.Qualify, 6);
        Assert.Equal(1.0, portugal.ReachRoundOf16, 6);
        Assert.Equal(0.0, croatia.ReachRoundOf16, 6);
        Assert.Equal(0.0, croatia.WinTournament, 6);
        Assert.Equal(1.0, projection.Teams.Sum(t => t.WinTournament), 6);
        Assert.All(projection.Teams, team =>
        {
            Assert.True(team.ReachRoundOf16 + 1e-9 >= team.ReachQuarterFinal);
            Assert.True(team.ReachQuarterFinal + 1e-9 >= team.ReachSemiFinal);
            Assert.True(team.ReachSemiFinal + 1e-9 >= team.ReachFinal);
            Assert.True(team.ReachFinal + 1e-9 >= team.WinTournament);
        });
    }

    [Fact]
    public async Task Simulation_AlwaysUsesRealKnockoutWinner()
    {
        await using var db = await ImportedDb();
        await SeedRoundOf32Async(db);
        UpsertKnockout(db, 83, KnockoutStageEnum.RoundOf32, "portugal", "croatia", isPlayed: true, winner: "croatia", homeGoals: 0, awayGoals: 1);
        UpsertKnockout(db, 84, KnockoutStageEnum.RoundOf32, "spain", "belgium", isPlayed: true, winner: "spain", homeGoals: 2, awayGoals: 0);
        UpsertKnockout(db, 93, KnockoutStageEnum.RoundOf16, "croatia", "spain");
        await db.SaveChangesAsync();

        var projection = await Simulation(db, simulations: 20, seed: 7).RunAsync(saveSnapshot: false);
        var portugal = projection.Teams.Single(t => t.TeamId == "portugal");
        var croatia = projection.Teams.Single(t => t.TeamId == "croatia");

        Assert.Equal(0.0, portugal.ReachRoundOf16, 6);
        Assert.Equal(0.0, portugal.WinTournament, 6);
        Assert.Equal(1.0, croatia.Qualify, 6);
        Assert.Equal(1.0, croatia.ReachRoundOf16, 6);
    }

    private static SimulationService Simulation(OloraculoDbContext db, int simulations, int seed)
    {
        var options = SimulationOptions(simulations, seed);
        var prediction = new PredictionService(db, options);
        var snapshots = new SnapshotService(db);
        return new SimulationService(db, prediction, snapshots, options);
    }

    private static async Task SeedRoundOf32Async(OloraculoDbContext db)
    {
        var teamIds = (await db.Groups.ToListAsync())
            .SelectMany(g => g.TeamIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .ToList();
        var available = new Queue<string>(teamIds
            .Where(t => t is not "portugal" and not "croatia" and not "spain" and not "belgium"));

        for (var i = 0; i < WorldCup2026Bracket.RoundOf32.Count; i++)
        {
            var tie = WorldCup2026Bracket.RoundOf32[i];
            var home = available.Dequeue();
            var away = available.Dequeue();
            if (tie.Id == 83)
            {
                home = "portugal";
                away = "croatia";
            }
            if (tie.Id == 84)
            {
                home = "spain";
                away = "belgium";
            }

            UpsertKnockout(db, tie.Id, tie.Stage, home, away);
        }

        await db.SaveChangesAsync();
    }

    private static void UpsertKnockout(
        OloraculoDbContext db,
        int matchNumber,
        KnockoutStageEnum stage,
        string home,
        string away,
        bool isPlayed = false,
        string? winner = null,
        int? homeGoals = null,
        int? awayGoals = null)
    {
        var match = db.KnockoutMatches.Local.FirstOrDefault(m => m.MatchNumber == matchNumber) ??
            db.KnockoutMatches.Find(matchNumber);
        if (match is null)
        {
            match = new KnockoutMatch { MatchNumber = matchNumber };
            db.KnockoutMatches.Add(match);
        }

        match.Stage = stage;
        match.ConfirmedHomeTeamId = home;
        match.ConfirmedAwayTeamId = away;
        match.IsPlayed = isPlayed;
        match.WinnerTeamId = winner;
        match.HomeGoals = homeGoals;
        match.AwayGoals = awayGoals;
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

}
