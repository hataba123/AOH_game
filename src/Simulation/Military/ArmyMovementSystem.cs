using AOH.Game.Domain;
using AOH.Game.Simulation;
using AOH.Game.Map;

namespace AOH.Game.Simulation.Military;

public sealed class ArmyMovementSystem : IGameSystem
{
    public void Process(GameWorld world)
    {
        foreach (var army in world.Armies.Values)
        {
            if (army.Path.Count == 0)
            {
                continue;
            }

            army.AddMovementProgress(1d);
            while (army.Path.Count > 0)
            {
                var nextProvinceId = army.Path[0];
                if (!world.TryGetProvince(nextProvinceId, out var nextProvince) ||
                    nextProvince.ControllerCountryId != army.OwnerCountryId ||
                    !world.ProvinceGraph.AreNeighbors(army.CurrentProvinceId, nextProvinceId))
                {
                    army.CancelMovement();
                    break;
                }

                var movementCost = ProvincePathfinder.GetMovementCost(nextProvince.Terrain);
                if (army.MovementProgress < movementCost)
                {
                    break;
                }

                if (!army.MoveToNextProvince(nextProvinceId, movementCost))
                {
                    break;
                }
            }
        }
    }
}
