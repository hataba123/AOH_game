using AOH.Game.Core;
using AOH.Game.Domain;
using AOH.Game.Domain.Armies;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Diplomacy;
using AOH.Game.Domain.Provinces;
using AOH.Game.Map;
using AOH.Game.Simulation.Military;
using Xunit;

namespace AOH.Tests;

public class WarAndCombatTests
{
    [Fact]
    public void War_DeclaresOnceAndPeaceResolvesOccupiedTerritory()
    {
        var world = CreateWorld();
        var attackerId = new CountryId(1);
        var defenderId = new CountryId(2);
        Assert.True(world.TryDeclareWar(attackerId, defenderId, new DateOnly(1444, 1, 1), out var war));
        Assert.NotNull(war);
        Assert.True(world.IsAtWar(attackerId, defenderId));
        Assert.False(world.TryDeclareWar(attackerId, defenderId, new DateOnly(1444, 1, 1), out _));

        var army = new Army(new ArmyId(1), attackerId, new ProvinceId(1), 1_000);
        Assert.True(world.TryAddArmy(army));
        army.OrderMovement(new ProvinceId(2), [new ProvinceId(2)]);
        new ArmyMovementSystem().Process(world);
        new CombatSystem(new GameRandom(4)).Process(world);
        Assert.Equal(attackerId, world.Provinces[new ProvinceId(2)].ControllerCountryId);
        Assert.Equal(defenderId, world.Provinces[new ProvinceId(2)].OwnerCountryId);

        war!.AddWarScore(attackerId, 20d);
        Assert.True(world.TryConcludePeace(war.Id, attackerId));

        Assert.False(world.IsAtWar(attackerId, defenderId));
        Assert.Equal(attackerId, world.Provinces[new ProvinceId(2)].OwnerCountryId);
        Assert.Equal(attackerId, world.Provinces[new ProvinceId(2)].ControllerCountryId);
    }

    [Fact]
    public void CombatSystem_IsDeterministicForTheSameSeedAndInitialState()
    {
        var first = SimulateBattle(12345);
        var second = SimulateBattle(12345);

        Assert.Equal(first, second);
    }

    private static (int AttackerSoldiers, int DefenderSoldiers, double WarScore, uint RandomState) SimulateBattle(int seed)
    {
        var world = CreateWorld();
        Assert.True(world.TryDeclareWar(new CountryId(1), new CountryId(2), new DateOnly(1444, 1, 1), out var war));
        var attacker = new Army(new ArmyId(1), new CountryId(1), new ProvinceId(1), 1_000);
        var defender = new Army(new ArmyId(2), new CountryId(2), new ProvinceId(1), 1_000);
        Assert.True(world.TryAddArmy(attacker));
        Assert.True(world.TryAddArmy(defender));
        var random = new GameRandom(seed);

        new CombatSystem(random).Process(world);

        return (attacker.Soldiers, defender.Soldiers, war!.AttackerWarScore, random.State);
    }

    private static GameWorld CreateWorld()
    {
        var countryOne = new Country(new CountryId(1), "An Lưu", "#C96E55", false, 1, 1_000);
        var countryTwo = new Country(new CountryId(2), "Dạ Lam", "#5D9C90", true, 2, 1_000);
        var provinces = new[]
        {
            CreateProvince(1, countryOne.Id, countryOne.Id, 0),
            CreateProvince(2, countryTwo.Id, countryTwo.Id, 10)
        };
        var graph = new ProvinceGraph(new Dictionary<ProvinceId, ProvinceId[]>
        {
            [new ProvinceId(1)] = [new ProvinceId(2)],
            [new ProvinceId(2)] = [new ProvinceId(1)]
        });

        return new GameWorld([countryOne, countryTwo], provinces, graph);
    }

    private static Province CreateProvince(int id, CountryId owner, CountryId controller, float x)
    {
        return new Province(
            new ProvinceId(id),
            $"Tỉnh {id}",
            owner,
            controller,
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
