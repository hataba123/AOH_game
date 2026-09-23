using AOH.Game.Domain;

namespace AOH.Game.Simulation.Population;

public sealed class PopulationSystem : IGameSystem
{
    private const double AnnualGrowthRate = 0.006d;
    private const double DaysPerYear = 365d;
    private readonly Dictionary<AOH.Game.Domain.Provinces.ProvinceId, double> _growthRemainders = [];

    public void Process(GameWorld world)
    {
        foreach (var province in world.Provinces.Values)
        {
            _growthRemainders.TryGetValue(province.Id, out var remainder);
            var fractionalGrowth = province.Population * AnnualGrowthRate / DaysPerYear + remainder;
            var dailyGrowth = (int)fractionalGrowth;
            _growthRemainders[province.Id] = fractionalGrowth - dailyGrowth;
            if (dailyGrowth <= 0)
            {
                continue;
            }

            var manpowerGrowth = province.ApplyPopulationGrowth(dailyGrowth);
            var owner = world.Countries[province.OwnerCountryId];
            owner.AddDemographics(dailyGrowth, manpowerGrowth);
        }
    }
}
