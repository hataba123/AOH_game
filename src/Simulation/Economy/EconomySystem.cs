using AOH.Game.Domain;
using AOH.Game.Domain.Countries;

namespace AOH.Game.Simulation.Economy;

public sealed class EconomySystem : IGameSystem
{
    public void Process(GameWorld world)
    {
        var incomeByCountry = new Dictionary<CountryId, double>(world.Countries.Count);
        foreach (var country in world.Countries.Values)
        {
            incomeByCountry.Add(country.Id, 0d);
        }

        foreach (var province in world.Provinces.Values)
        {
            var dailyIncome = province.Population * province.Development * province.TaxRate / 365d;
            incomeByCountry[province.OwnerCountryId] += dailyIncome;
        }

        foreach (var country in world.Countries.Values)
        {
            incomeByCountry.TryGetValue(country.Id, out var income);
            country.ApplyDailyEconomy(income, 0d);
        }
    }
}
