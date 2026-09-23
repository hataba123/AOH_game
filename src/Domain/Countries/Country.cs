namespace AOH.Game.Domain.Countries;

public sealed class Country
{
    public Country(
        CountryId id,
        string name,
        string mapColor,
        bool isAiControlled,
        int capitalProvinceId)
    {
        Id = id;
        Name = name;
        MapColor = mapColor;
        IsAiControlled = isAiControlled;
        CapitalProvinceId = capitalProvinceId;
    }

    public CountryId Id { get; }

    public string Name { get; }

    public string MapColor { get; }

    public bool IsAiControlled { get; }

    public int CapitalProvinceId { get; }
}
