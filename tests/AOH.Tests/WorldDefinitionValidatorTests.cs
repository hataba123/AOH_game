using AOH.Game.Domain;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;
using AOH.Game.Map;
using Xunit;

namespace AOH.Tests;

public class WorldDefinitionValidatorTests
{
    [Fact]
    public void Validate_DetectsDuplicateCountryId()
    {
        var countries = new[]
        {
            new Country(new CountryId(1), "Đại Việt", "#9E2A2B", false, 1),
            new Country(new CountryId(1), "Lan Xang", "#2A6F4E", true, 2)
        };

        var provinces = new[]
        {
            new Province(new ProvinceId(1), "Thăng Long", new CountryId(1), new CountryId(1), 1000, 10, 5, 0.1, 100, ProvinceTerrain.Plains, false, new MapPoint(0,0), [new MapPoint(0,0), new MapPoint(1,0), new MapPoint(0,1)]),
            new Province(new ProvinceId(2), "Luang Prabang", new CountryId(1), new CountryId(1), 1000, 10, 5, 0.1, 100, ProvinceTerrain.Plains, false, new MapPoint(0,0), [new MapPoint(0,0), new MapPoint(1,0), new MapPoint(0,1)])
        };

        var adjacency = new Dictionary<ProvinceId, ProvinceId[]>
        {
            [new ProvinceId(1)] = [new ProvinceId(2)],
            [new ProvinceId(2)] = [new ProvinceId(1)]
        };

        var colorLookup = new ProvinceColorLookup(provinces.Select(p => p.Id));
        var mapData = new ProvinceMapData(1, 1, [0, 0, 1], new Rgb24(21, 47, 55));

        var errors = WorldDefinitionValidator.Validate(countries, provinces, adjacency, colorLookup, mapData);

        Assert.Contains(errors, e => e.Contains("Country ID trùng"));
    }

    [Fact]
    public void Validate_DetectsAsymmetricAdjacency()
    {
        var countries = new[]
        {
            new Country(new CountryId(1), "Đại Việt", "#9E2A2B", false, 1)
        };

        var provinces = new[]
        {
            new Province(new ProvinceId(1), "Thăng Long", new CountryId(1), new CountryId(1), 1000, 10, 5, 0.1, 100, ProvinceTerrain.Plains, false, new MapPoint(0,0), [new MapPoint(0,0), new MapPoint(1,0), new MapPoint(0,1)]),
            new Province(new ProvinceId(2), "Đông Triều", new CountryId(1), new CountryId(1), 1000, 10, 5, 0.1, 100, ProvinceTerrain.Plains, false, new MapPoint(0,0), [new MapPoint(0,0), new MapPoint(1,0), new MapPoint(0,1)])
        };

        // 1 connects to 2, but 2 does NOT connect back to 1!
        var adjacency = new Dictionary<ProvinceId, ProvinceId[]>
        {
            [new ProvinceId(1)] = [new ProvinceId(2)],
            [new ProvinceId(2)] = []
        };

        var colorLookup = new ProvinceColorLookup(provinces.Select(p => p.Id));
        var mapData = new ProvinceMapData(1, 1, [0, 0, 1], new Rgb24(21, 47, 55));

        var errors = WorldDefinitionValidator.Validate(countries, provinces, adjacency, colorLookup, mapData);

        Assert.Contains(errors, e => e.Contains("chưa đối xứng"));
    }

    [Fact]
    public void Validate_DetectsSelfLoopConnection()
    {
        var countries = new[]
        {
            new Country(new CountryId(1), "Đại Việt", "#9E2A2B", false, 1)
        };

        var provinces = new[]
        {
            new Province(new ProvinceId(1), "Thăng Long", new CountryId(1), new CountryId(1), 1000, 10, 5, 0.1, 100, ProvinceTerrain.Plains, false, new MapPoint(0,0), [new MapPoint(0,0), new MapPoint(1,0), new MapPoint(0,1)])
        };

        // 1 connects to 1
        var adjacency = new Dictionary<ProvinceId, ProvinceId[]>
        {
            [new ProvinceId(1)] = [new ProvinceId(1)]
        };

        var colorLookup = new ProvinceColorLookup(provinces.Select(p => p.Id));
        var mapData = new ProvinceMapData(1, 1, [0, 0, 1], new Rgb24(21, 47, 55));

        var errors = WorldDefinitionValidator.Validate(countries, provinces, adjacency, colorLookup, mapData);

        Assert.Contains(errors, e => e.Contains("không thể kết nối với chính nó"));
    }
}
