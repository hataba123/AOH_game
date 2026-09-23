using AOH.Game.Core;
using AOH.Game.Domain;
using AOH.Game.Domain.Armies;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Diplomacy;
using AOH.Game.Domain.Provinces;

namespace AOH.Game.Simulation.Military;

public sealed class CombatSystem : IGameSystem
{
    private readonly GameRandom _random;

    public CombatSystem(GameRandom random)
    {
        _random = random;
    }

    public void Process(GameWorld world)
    {
        var armiesByProvince = new Dictionary<ProvinceId, List<Army>>();
        foreach (var army in world.Armies.Values)
        {
            if (!armiesByProvince.TryGetValue(army.CurrentProvinceId, out var armies))
            {
                armies = [];
                armiesByProvince.Add(army.CurrentProvinceId, armies);
            }

            armies.Add(army);
        }

        foreach (var location in armiesByProvince)
        {
            if (!world.TryGetProvince(location.Key, out var province))
            {
                continue;
            }

            foreach (var war in world.Wars.Values.Where(war => war.IsActive))
            {
                var attackers = location.Value.Where(army => war.AttackerIds.Contains(army.OwnerCountryId)).ToArray();
                var defenders = location.Value.Where(army => war.DefenderIds.Contains(army.OwnerCountryId)).ToArray();

                if (attackers.Length > 0 && defenders.Length > 0)
                {
                    ResolveBattle(world, province, war, attackers, defenders);
                }
                else if (attackers.Length > 0)
                {
                    TryOccupyProvince(province, war, attackers[0].OwnerCountryId);
                }
                else if (defenders.Length > 0)
                {
                    TryOccupyProvince(province, war, defenders[0].OwnerCountryId);
                }
            }
        }
    }

    private void ResolveBattle(GameWorld world, Province province, War war, IReadOnlyList<Army> attackers, IReadOnlyList<Army> defenders)
    {
        var attackerPower = CalculatePower(attackers) * _random.NextDouble(0.9d, 1.1d);
        var terrainModifier = province.Terrain switch
        {
            ProvinceTerrain.Highlands => 1.25d,
            ProvinceTerrain.Forest => 1.15d,
            ProvinceTerrain.Marsh => 1.2d,
            _ => 1d
        };
        var defenderPower = CalculatePower(defenders) * terrainModifier * _random.NextDouble(0.9d, 1.1d);
        if (attackerPower <= 0d || defenderPower <= 0d)
        {
            return;
        }

        var attackerLossRatio = Math.Clamp(0.025d * defenderPower / attackerPower, 0.01d, 0.12d);
        var defenderLossRatio = Math.Clamp(0.025d * attackerPower / defenderPower, 0.01d, 0.12d);
        var initialAttackerSoldiers = attackers.Sum(army => army.Soldiers);
        var initialDefenderSoldiers = defenders.Sum(army => army.Soldiers);
        var attackerLosses = ApplyLosses(world, attackers, attackerLossRatio);
        var defenderLosses = ApplyLosses(world, defenders, defenderLossRatio);

        var attackerLossRate = attackerLosses / (double)initialAttackerSoldiers;
        var defenderLossRate = defenderLosses / (double)initialDefenderSoldiers;
        var casualtyDifference = defenderLossRate - attackerLossRate;
        if (casualtyDifference > 0d)
        {
            war.AddWarScore(war.AttackerIds.First(), Math.Clamp(0.25d + (casualtyDifference * 12d), 0.25d, 3d));
        }
        else if (casualtyDifference < 0d)
        {
            war.AddWarScore(war.DefenderIds.First(), Math.Clamp(0.25d + (Math.Abs(casualtyDifference) * 12d), 0.25d, 3d));
        }

        var attackersRemain = attackers.Any(army => army.Soldiers > 0);
        var defendersRemain = defenders.Any(army => army.Soldiers > 0);
        if (attackersRemain != defendersRemain)
        {
            var victor = attackersRemain ? war.AttackerIds.First() : war.DefenderIds.First();
            TryOccupyProvince(province, war, victor);
        }
    }

    private static double CalculatePower(IEnumerable<Army> armies)
    {
        var power = 0d;
        foreach (var army in armies)
        {
            power += army.Soldiers * army.Attack * army.Morale * army.Organization;
        }

        return power;
    }

    private static int ApplyLosses(GameWorld world, IEnumerable<Army> armies, double lossRatio)
    {
        var totalLosses = 0;
        foreach (var army in armies)
        {
            totalLosses += army.ApplyCasualties(lossRatio);
            if (army.Soldiers <= 0)
            {
                world.TryRemoveArmy(army.Id);
            }
        }

        return totalLosses;
    }

    private static void TryOccupyProvince(Province province, War war, CountryId occupierCountryId)
    {
        if (province.OwnerCountryId == occupierCountryId)
        {
            province.SetController(occupierCountryId);
            return;
        }

        if (war.AreEnemies(occupierCountryId, province.OwnerCountryId) && province.ControllerCountryId != occupierCountryId)
        {
            province.SetController(occupierCountryId);
            war.AddWarScore(occupierCountryId, 8d);
        }
    }
}
