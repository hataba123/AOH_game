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

    public CountryId OwnerCountryId { get; private set; }

    public CountryId ControllerCountryId { get; private set; }

    public int Population { get; private set; }

    public double Economy { get; }

    public double Development { get; }

    public double TaxRate { get; }

    public int Manpower { get; private set; }

    public ProvinceTerrain Terrain { get; }

    public bool IsCoastal { get; }

    public MapPoint CapitalPosition { get; }

    public IReadOnlyList<MapPoint> Polygon { get; }

    internal int ApplyPopulationGrowth(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        Population += amount;
        var newManpower = (int)(Population * 0.075d);
        var manpowerGrowth = Math.Max(0, newManpower - Manpower);
        Manpower += manpowerGrowth;
        return manpowerGrowth;
    }

    internal bool TryRecruitSoldiers(int soldiers)
    {
        if (soldiers <= 0 || Manpower < soldiers)
        {
            return false;
        }

        Manpower -= soldiers;
        return true;
    }

    internal void SetController(CountryId countryId) => ControllerCountryId = countryId;

    internal void TransferOwnership(CountryId countryId)
    {
        OwnerCountryId = countryId;
        ControllerCountryId = countryId;
    }
}
