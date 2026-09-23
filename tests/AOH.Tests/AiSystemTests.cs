using AOH.Game.Core;
using AOH.Game.Domain;
using AOH.Game.Domain.Armies;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;
using AOH.Game.Simulation.AI;
using Xunit;

namespace AOH.Tests;

public class AiSystemTests
{
    [Fact]
    public void AiSystem_ThinksEverySevenDaysAndRecruitsWhenItNeedsAnArmy()
    {
        var world = CreateWorld();
        var ai = new AiSystem(new GameTime(new DateOnly(1444, 1, 1)), new GameRandom(17));

        for (var day = 0; day < 6; day++)
        {
            ai.Process(world);
        }

        Assert.Empty(world.Armies);
        ai.Process(world);

        Assert.Single(world.Armies);
        Assert.Equal(AiActionType.RecruitArmy, Assert.Single(ai.LastDecisions).Action);
        Assert.Equal(new CountryId(2), world.Armies.Values.Single().OwnerCountryId);
    }

    [Fact]
    public void AiSystem_AttacksAnEnemyProvinceDuringWar()
    {
        var world = CreateWorld();
        Assert.True(world.TryDeclareWar(new CountryId(2), new CountryId(1), new DateOnly(1444, 1, 1), out _));
        var army = new Army(new ArmyId(1), new CountryId(2), new ProvinceId(2), 1_000);
        Assert.True(world.TryAddArmy(army));
        var ai = new AiSystem(new GameTime(new DateOnly(1444, 1, 1)), new GameRandom(8));

        for (var day = 0; day < 7; day++)
        {
            ai.Process(world);
        }

        var decision = Assert.Single(ai.LastDecisions);
        Assert.Equal(AiActionType.AttackProvince, decision.Action);
        Assert.Equal(new ProvinceId(1), army.Path[^1]);
    }

    [Fact]
    public void AiSystem_DeclaresWarWhenItHasEnoughMilitaryAndMoney()
    {
        var world = CreateWorld();
        Assert.True(world.TryAddArmy(new Army(new ArmyId(1), new CountryId(2), new ProvinceId(2), 1_000)));
        Assert.True(world.TryAddArmy(new Army(new ArmyId(2), new CountryId(2), new ProvinceId(2), 1_000)));
        var ai = new AiSystem(new GameTime(new DateOnly(1444, 1, 1)), new GameRandom(11));

        for (var day = 0; day < 7; day++)
        {
            ai.Process(world);
        }

        Assert.Equal(AiActionType.DeclareWar, Assert.Single(ai.LastDecisions).Action);
        Assert.True(world.IsAtWar(new CountryId(2), new CountryId(1)));
    }

    [Fact]
    public void AiSystem_RepeatsTheSameDecisionForTheSameSeedAndWorld()
    {
        var firstWorld = CreateWorld();
        var secondWorld = CreateWorld();
        var firstAi = new AiSystem(new GameTime(new DateOnly(1444, 1, 1)), new GameRandom(2026));
        var secondAi = new AiSystem(new GameTime(new DateOnly(1444, 1, 1)), new GameRandom(2026));

        for (var day = 0; day < 7; day++)
        {
            firstAi.Process(firstWorld);
            secondAi.Process(secondWorld);
        }

        Assert.Equal(firstAi.LastDecisions, secondAi.LastDecisions);
        Assert.Equal(firstWorld.Armies.Values.Single().CurrentProvinceId, secondWorld.Armies.Values.Single().CurrentProvinceId);
    }

    private static GameWorld CreateWorld()
    {
        var player = new Country(new CountryId(1), "An Lưu", "#C96E55", false, 1, 1_000);
        var ai = new Country(new CountryId(2), "Dạ Lam", "#5D9C90", true, 2, 5_000);
        var provinces = new[]
        {
            CreateProvince(1, player.Id, 0),
            CreateProvince(2, ai.Id, 10)
        };
        var graph = new ProvinceGraph(new Dictionary<ProvinceId, ProvinceId[]>
        {
            [new ProvinceId(1)] = [new ProvinceId(2)],
            [new ProvinceId(2)] = [new ProvinceId(1)]
        });
        return new GameWorld([player, ai], provinces, graph);
    }

    private static Province CreateProvince(int id, CountryId countryId, float x)
    {
        return new Province(
            new ProvinceId(id),
            $"Tỉnh {id}",
            countryId,
            countryId,
            population: 100_000,
            economy: 1d,
            development: 1d,
            taxRate: 0.1d,
            manpower: 7_500,
            ProvinceTerrain.Plains,
            isCoastal: false,
            new MapPoint(x, 0),
            [new MapPoint(x, 0), new MapPoint(x + 1, 0), new MapPoint(x + 1, 1)]);
    }
}
