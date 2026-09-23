using AOH.Game.Domain.Countries;

namespace AOH.Game.Domain.Provinces;

public sealed class Province
{
    public Province(
        ProvinceId id,
        string name,
        CountryId ownerCountryId,
        CountryId controllerCountryId,
        int population,
        double economy,
        double development,
        double taxRate,
        int manpower,
        ProvinceTerrain terrain,
        bool isCoastal,
        MapPoint capitalPosition,
        IReadOnlyList<MapPoint> polygon)
    {
        Id = id;
        Name = name;
        OwnerCountryId = ownerCountryId;
        ControllerCountryId = controllerCountryId;
        Population = population;
        Economy = economy;
        Development = development;
        TaxRate = taxRate;
        Manpower = manpower;
        Terrain = terrain;
        IsCoastal = isCoastal;
        CapitalPosition = capitalPosition;
        Polygon = polygon;
    }

    public ProvinceId Id { get; }

    public string Name { get; }

    public CountryId OwnerCountryId { get; }

    public CountryId ControllerCountryId { get; }

    public int Population { get; }

    public double Economy { get; }

    public double Development { get; }

    public double TaxRate { get; }

    public int Manpower { get; }

    public ProvinceTerrain Terrain { get; }

    public bool IsCoastal { get; }

    public MapPoint CapitalPosition { get; }

    public IReadOnlyList<MapPoint> Polygon { get; }
}
