using System.Collections.ObjectModel;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;

namespace AOH.Game.Domain;

public sealed class GameWorld
{
    public GameWorld(
        IEnumerable<Country> countries,
        IEnumerable<Province> provinces,
        ProvinceGraph provinceGraph)
    {
        Countries = new ReadOnlyDictionary<CountryId, Country>(countries.ToDictionary(country => country.Id));
        Provinces = new ReadOnlyDictionary<ProvinceId, Province>(provinces.ToDictionary(province => province.Id));
        ProvinceGraph = provinceGraph;
    }

    public IReadOnlyDictionary<CountryId, Country> Countries { get; }

    public IReadOnlyDictionary<ProvinceId, Province> Provinces { get; }

    public ProvinceGraph ProvinceGraph { get; }

    public bool TryGetProvince(ProvinceId provinceId, out Province province)
    {
        return Provinces.TryGetValue(provinceId, out province!);
    }

    public bool TryGetCountry(CountryId countryId, out Country country)
    {
        return Countries.TryGetValue(countryId, out country!);
    }
}
