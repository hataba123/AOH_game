using AOH.Game.Domain;
using AOH.Game.Domain.Armies;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;
using AOH.Game.Map;
using AOH.Game.Simulation.Military;
using Xunit;

namespace AOH.Tests;

public class MilitarySystemTests
{
    [Fact]
    public void Pathfinder_FindsLowestMovementCostRoute()
    {
        var world = CreateWorld(
            [ProvinceTerrain.Plains, ProvinceTerrain.Plains, ProvinceTerrain.Marsh, ProvinceTerrain.Plains],
            [
                [2, 3],
                [1, 4],
                [1, 4],
                [2, 3]
            ]);

        var path = new ProvincePathfinder(world).FindPath(new ProvinceId(1), new ProvinceId(4), new CountryId(1));

        Assert.Equal([new ProvinceId(1), new ProvinceId(2), new ProvinceId(4)], path);
    }

    [Fact]
    public void Pathfinder_DoesNotRouteThroughProvinceControlledByAnotherCountry()
    {
        var world = CreateWorld(
            [ProvinceTerrain.Plains, ProvinceTerrain.Plains, ProvinceTerrain.Plains],
            [[2], [1, 3], [2]],
            [1, 2, 2]);

        var path = new ProvincePathfinder(world).FindPath(new ProvinceId(1), new ProvinceId(3), new CountryId(1));

        Assert.Empty(path);
    }

    [Fact]
    public void ArmyMovementSystem_AdvancesAlongPathWithoutTeleporting()
    {
        var world = CreateWorld(
            [ProvinceTerrain.Plains, ProvinceTerrain.Forest, ProvinceTerrain.Plains],
            [[2], [1, 3], [2]]);
        var path = new ProvincePathfinder(world).FindPath(new ProvinceId(1), new ProvinceId(3), new CountryId(1));
        var army = new Army(new ArmyId(1), new CountryId(1), new ProvinceId(1), 1_000);
        army.OrderMovement(new ProvinceId(3), path.Skip(1).ToArray());
        Assert.True(world.TryAddArmy(army));
        var system = new ArmyMovementSystem();

        system.Process(world);
        Assert.Equal(new ProvinceId(1), army.CurrentProvinceId);
        system.Process(world);
        Assert.Equal(new ProvinceId(2), army.CurrentProvinceId);
        system.Process(world);

        Assert.Equal(new ProvinceId(3), army.CurrentProvinceId);
        Assert.Empty(army.Path);
        Assert.Null(army.TargetProvinceId);
    }

    [Fact]
    public void Recruitment_DeductsTreasuryAndManpowerAndCreatesArmy()
    {
        var world = CreateWorld([ProvinceTerrain.Plains], [[]], population: 100_000, treasury: 2_000);

        var result = new ArmyRecruitmentService().Recruit(world, new ProvinceId(1), new CountryId(1), 1_000);

        Assert.True(result.Success);
        Assert.NotNull(result.Army);
        Assert.Equal(1_500d, world.Countries[new CountryId(1)].Treasury);
        Assert.Equal(6_500, world.Countries[new CountryId(1)].Manpower);
        Assert.Equal(6_500, world.Provinces[new ProvinceId(1)].Manpower);
        Assert.Single(world.Armies);
    }

    [Fact]
    public void Recruitment_WhenInsufficientManpower_DoesNotSpendTreasury()
    {
        var world = CreateWorld([ProvinceTerrain.Plains], [[]], population: 10_000, treasury: 2_000);

        var result = new ArmyRecruitmentService().Recruit(world, new ProvinceId(1), new CountryId(1), 1_000);

        Assert.False(result.Success);
        Assert.Equal(2_000d, world.Countries[new CountryId(1)].Treasury);
        Assert.Empty(world.Armies);
    }

    private static GameWorld CreateWorld(
        ProvinceTerrain[] terrains,
        int[][] adjacency,
        int[]? controllers = null,
        int population = 100_000,
        double treasury = 1_000)
    {
        controllers ??= Enumerable.Repeat(1, terrains.Length).ToArray();
        var countryIds = controllers.Append(1).Distinct().ToArray();
        var countries = countryIds.Select(id => new Country(
            new CountryId(id),
            $"Quốc gia {id}",
            id == 1 ? "#C96E55" : "#5D9C90",
            id != 1,
            id == 1 ? 1 : 3,
            id == 1 ? treasury : 0d)).ToArray();
        var provinces = terrains.Select((terrain, index) => new Province(
            new ProvinceId(index + 1),
            $"Tỉnh {index + 1}",
            new CountryId(controllers[index]),
            new CountryId(controllers[index]),
            population,
            economy: 1d,
            development: 1d,
            taxRate: 0.1d,
            manpower: (int)(population * 0.075d),
            terrain,
            isCoastal: false,
            new MapPoint(index * 10, (index % 2) * 10),
            [new MapPoint(0, 0), new MapPoint(1, 0), new MapPoint(1, 1)])).ToArray();
        var graph = new ProvinceGraph(adjacency.Select((neighbors, index) => new KeyValuePair<ProvinceId, ProvinceId[]>(
            new ProvinceId(index + 1),
            neighbors.Select(neighbor => new ProvinceId(neighbor)).ToArray())).ToDictionary(pair => pair.Key, pair => pair.Value));
        return new GameWorld(countries, provinces, graph);
    }
}
