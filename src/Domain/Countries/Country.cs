namespace AOH.Game.Domain.Countries;

public sealed class Country
{
    public Country(
        CountryId id,
        string name,
        string mapColor,
        bool isAiControlled,
        int capitalProvinceId,
        double treasury = 0d)
    {
        Id = id;
        Name = name;
        MapColor = mapColor;
        IsAiControlled = isAiControlled;
        CapitalProvinceId = capitalProvinceId;
        Treasury = treasury;
    }

    public CountryId Id { get; }

    public string Name { get; }

    public string MapColor { get; }

    public bool IsAiControlled { get; }

    public int CapitalProvinceId { get; }

    public double Treasury { get; private set; }

    public double Income { get; private set; }

    public double Expenses { get; private set; }

    public long Population { get; private set; }

    public int Manpower { get; private set; }

    internal void SetDemographics(long population, int manpower)
    {
        Population = population;
        Manpower = manpower;
    }

    internal void AddDemographics(int populationGrowth, int manpowerGrowth)
    {
        Population += populationGrowth;
        Manpower += manpowerGrowth;
    }

    internal void ApplyDailyEconomy(double income, double expenses)
    {
        Income = income;
        Expenses = expenses;
        Treasury += income - expenses;
    }

    internal bool TryRecruitSoldiers(int soldiers, double cost)
    {
        if (soldiers <= 0 || cost < 0d || Manpower < soldiers || Treasury < cost)
        {
            return false;
        }

        Manpower -= soldiers;
        Treasury -= cost;
        return true;
    }
}
