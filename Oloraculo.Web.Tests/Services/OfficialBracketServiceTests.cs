using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Oloraculo.Web;
using Oloraculo.Web.DAL;
using Oloraculo.Web.Models;
using Oloraculo.Web.Services;

namespace Oloraculo.Web.Tests;

public class OfficialBracketServiceTests : TestFixtures
{
    [Fact]
    public async Task OfficialBracket_ReturnsPendingWhenNoMappedKnockoutFixturesExist()
    {
        await using var db = await NewDb();
        var service = Service(db);

        var projection = await service.BuildAsync();

        Assert.False(projection.HasOfficialFixtures);
        Assert.Empty(projection.Ties);
        Assert.Equal(OfficialBracketService.PendingMessage, projection.PendingMessage);
    }

    [Fact]
    public async Task OfficialBracket_IgnoresUnmappedLegacyKnockoutFixtures()
    {
        await using var db = await NewDb();
        db.Fixtures.Add(new Fixture { Id = "ko:73", Group = "KO", HomeTeamId = "germany", AwayTeamId = "bosnia-and-herzegovina" });
        await db.SaveChangesAsync();
        var service = Service(db);

        var projection = await service.BuildAsync();

        Assert.False(projection.HasOfficialFixtures);
        Assert.Empty(projection.Ties);
    }

    [Fact]
    public async Task OfficialBracket_UsesMappedOfficialFixtureAsCurrentPhase()
    {
        await using var db = await NewDb();
        db.Teams.AddRange(
            new Team { Id = "germany", Name = "Germany" },
            new Team { Id = "paraguay", Name = "Paraguay" });
        db.Ratings.AddRange(
            new Rating { TeamId = "germany", Type = RatingTypeEnum.Elo, Value = 1900, Source = "test", AsOf = DateTimeOffset.UtcNow },
            new Rating { TeamId = "paraguay", Type = RatingTypeEnum.Elo, Value = 1650, Source = "test", AsOf = DateTimeOffset.UtcNow },
            new Rating { TeamId = "germany", Type = RatingTypeEnum.Fifa, Value = 5, Source = "test", AsOf = DateTimeOffset.UtcNow },
            new Rating { TeamId = "paraguay", Type = RatingTypeEnum.Fifa, Value = 40, Source = "test", AsOf = DateTimeOffset.UtcNow });
        db.Fixtures.Add(new Fixture { Id = "ko:73", Group = "KO", KnockoutMatchNumber = 74, HomeTeamId = "germany", AwayTeamId = "paraguay", NeutralVenue = true });
        db.ApiMappings.Add(new ApiMapping { LocalFixtureId = "ko:73", ExternalFixtureId = "730", UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var service = Service(db);

        var projection = await service.BuildAsync();
        var tie = Assert.Single(projection.Ties);

        Assert.True(projection.HasOfficialFixtures);
        Assert.Equal("ko:73", tie.FixtureId);
        Assert.Equal(74, tie.TieId);
        Assert.True(tie.IsOfficialFixture);
        Assert.Equal("germany", tie.HomeTeamId);
        Assert.Equal("paraguay", tie.AwayTeamId);
        Assert.NotNull(tie.Prediction);
        Assert.NotEqual(tie.HomeAdvanceProbability, tie.Prediction!.Outcome.HomeWin);
    }

    [Fact]
    public async Task OfficialBracket_UsesActualResultsAndPublishedLaterFixtureWithoutDuplicatingTeams()
    {
        await using var db = await NewDb();
        var roundOf32 = new (int Match, string Home, string Away)[]
        {
            (73, "south-africa", "canada"), (74, "germany", "paraguay"),
            (75, "netherlands", "morocco"), (76, "brazil", "japan"),
            (77, "france", "sweden"), (78, "ivory-coast", "norway"),
            (79, "mexico", "ecuador"), (80, "england", "congo-dr"),
            (81, "united-states", "bosnia-and-herzegovina"), (82, "belgium", "senegal"),
            (83, "portugal", "croatia"), (84, "spain", "austria"),
            (85, "switzerland", "algeria"), (86, "argentina", "cape-verde"),
            (87, "colombia", "ghana"), (88, "australia", "egypt")
        };
        var teamIds = roundOf32.SelectMany(match => new[] { match.Home, match.Away }).Distinct().ToList();
        db.Teams.AddRange(teamIds.Select(id => new Team { Id = id, Name = id }));
        db.Ratings.AddRange(teamIds.SelectMany((id, index) => new[]
        {
            new Rating { TeamId = id, Type = RatingTypeEnum.Elo, Value = 1800 - index, Source = "test", AsOf = DateTimeOffset.UtcNow },
            new Rating { TeamId = id, Type = RatingTypeEnum.Fifa, Value = index + 1, Source = "test", AsOf = DateTimeOffset.UtcNow }
        }));

        foreach (var match in roundOf32)
        {
            var fixture = new Fixture
            {
                Id = $"ko:{match.Match}",
                Group = "KO",
                KnockoutMatchNumber = match.Match,
                HomeTeamId = match.Home,
                AwayTeamId = match.Away,
                NeutralVenue = true
            };
            if (match.Match == 73)
            {
                fixture.IsPlayed = true;
                fixture.HomeGoals = 0;
                fixture.AwayGoals = 1;
                fixture.WinnerTeamId = "canada";
            }
            else if (match.Match == 75)
            {
                fixture.IsPlayed = true;
                fixture.HomeGoals = 1;
                fixture.AwayGoals = 1;
                fixture.WinnerTeamId = "morocco";
            }

            db.Fixtures.Add(fixture);
            db.ApiMappings.Add(new ApiMapping { LocalFixtureId = fixture.Id, ExternalFixtureId = match.Match.ToString(), UpdatedAt = DateTimeOffset.UtcNow });
        }

        db.Fixtures.Add(new Fixture
        {
            Id = "ko:legacy-wrong-89",
            Group = "KO",
            KnockoutMatchNumber = 90,
            HomeTeamId = "canada",
            AwayTeamId = "morocco",
            NeutralVenue = true
        });
        db.ApiMappings.Add(new ApiMapping { LocalFixtureId = "ko:legacy-wrong-89", ExternalFixtureId = "900", UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var projection = await Service(db).BuildAsync();

        Assert.Equal(31, projection.Ties.Count);
        Assert.Empty(projection.Warnings);
        var match90 = Assert.Single(projection.Ties, tie => tie.TieId == 90);
        Assert.True(match90.IsOfficialFixture);
        Assert.Equal("canada", match90.HomeTeamId);
        Assert.Equal("morocco", match90.AwayTeamId);
        Assert.Equal("canada", projection.Ties.Single(tie => tie.TieId == 73).ActualWinnerTeamId);
        Assert.Equal("morocco", projection.Ties.Single(tie => tie.TieId == 75).ActualWinnerTeamId);

        foreach (var stage in projection.Ties.GroupBy(tie => tie.StageLabel))
        {
            var teams = stage.SelectMany(tie => new[] { tie.HomeTeamId, tie.AwayTeamId }).ToList();
            Assert.Equal(teams.Count, teams.Distinct(StringComparer.Ordinal).Count());
        }
    }

    private static OfficialBracketService Service(OloraculoDbContext db)
    {
        var options = Options.Create(new OloraculoConfig { GoalModelYearsWindow = 3, RecentResultCount = 8 });
        return new OfficialBracketService(db, new PredictionService(db, options));
    }
}
