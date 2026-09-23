using AOH.Game.Domain;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;
using AOH.Game.Simulation.Economy;
using AOH.Game.Simulation.Population;
using Xunit;

namespace AOH.Tests;

public class EconomyAndPopulationSystemTests
{
    [Fact]
    public void EconomySystem_AddsProvinceTaxIncomeToCountryTreasury()
    {
        var (world, country, _) = CreateWorld(population: 365_000, treasury: 1_000);

        new EconomySystem().Process(world);

        Assert.Equal(200d, country.Income, precision: 6);
        Assert.Equal(1_200d, country.Treasury, precision: 6);
        Assert.Equal(0d, country.Expenses);
    }

    [Fact]
    public void PopulationSystem_AccumulatesFractionalDailyGrowthAcrossTheYear()
    {
        var (world, country, province) = CreateWorld(population: 100_000, treasury: 0);
        var system = new PopulationSystem();

        for (var day = 0; day < 365; day++)
        {
            system.Process(world);
        }

        Assert.InRange(province.Population, 100_600, 100_602);
        Assert.Equal(province.Population, country.Population);
        Assert.Equal(province.Manpower, country.Manpower);
    }

    private static (GameWorld World, Country Country, Province Province) CreateWorld(int population, double treasury)
    {
        var countryId = new CountryId(1);
        var provinceId = new ProvinceId(1);
        var country = new Country(countryId, "An Lưu", "#C96E55", false, provinceId.Value, treasury);
        var province = new Province(
            provinceId,
            "Bình Lưu",
            countryId,
            countryId,
            population,
            economy: 1d,
            development: 2d,
            taxRate: 0.1d,
            manpower: (int)(population * 0.075d),
            ProvinceTerrain.Plains,
            isCoastal: false,
            new MapPoint(0, 0),
            [new MapPoint(0, 0), new MapPoint(1, 0), new MapPoint(1, 1)]);
        var graph = new ProvinceGraph(new Dictionary<ProvinceId, ProvinceId[]>
        {
            [provinceId] = []
        });
        return (new GameWorld([country], [province], graph), country, province);
    }
}
