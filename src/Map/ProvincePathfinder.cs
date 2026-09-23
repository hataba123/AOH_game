using AOH.Game.Domain;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;

namespace AOH.Game.Map;

public sealed class ProvincePathfinder
{
    private readonly GameWorld _world;
    private readonly double _maximumEdgeLength;

    public ProvincePathfinder(GameWorld world)
    {
        _world = world;
        _maximumEdgeLength = FindMaximumEdgeLength(world);
    }

    public IReadOnlyList<ProvinceId> FindPath(ProvinceId start, ProvinceId target, CountryId countryId)
    {
        if (!_world.TryGetProvince(start, out var startProvince) ||
            !_world.TryGetProvince(target, out var targetProvince) ||
            startProvince.ControllerCountryId != countryId ||
            targetProvince.ControllerCountryId != countryId)
        {
            return Array.Empty<ProvinceId>();
        }

        if (start == target)
        {
            return [start];
        }

        var frontier = new PriorityQueue<ProvinceId, (double EstimatedTotalCost, int ProvinceId)>();
        var costs = new Dictionary<ProvinceId, double> { [start] = 0d };
        var previous = new Dictionary<ProvinceId, ProvinceId>();
        frontier.Enqueue(start, (Heuristic(start, target), start.Value));

        while (frontier.TryDequeue(out var current, out var priority))
        {
            if (!costs.TryGetValue(current, out var currentCost) ||
                priority.EstimatedTotalCost > currentCost + Heuristic(current, target) + 1e-9)
            {
                continue;
            }

            if (current == target)
            {
                return ReconstructPath(start, target, previous);
            }

            foreach (var neighborId in _world.ProvinceGraph.GetNeighbors(current))
            {
                if (!_world.TryGetProvince(neighborId, out var neighbor) || neighbor.ControllerCountryId != countryId)
                {
                    continue;
                }

                var nextCost = currentCost + GetMovementCost(neighbor.Terrain);
                if (costs.TryGetValue(neighborId, out var knownCost) && nextCost >= knownCost)
                {
                    continue;
                }

                costs[neighborId] = nextCost;
                previous[neighborId] = current;
                frontier.Enqueue(neighborId, (nextCost + Heuristic(neighborId, target), neighborId.Value));
            }
        }

        return Array.Empty<ProvinceId>();
    }

    public static double GetMovementCost(ProvinceTerrain terrain) => terrain switch
    {
        ProvinceTerrain.Plains => 1d,
        ProvinceTerrain.Forest => 1.25d,
        ProvinceTerrain.Coast => 1.15d,
        ProvinceTerrain.Marsh => 1.5d,
        ProvinceTerrain.Highlands => 1.75d,
        _ => 1d
    };

    private double Heuristic(ProvinceId currentId, ProvinceId targetId)
    {
        if (_maximumEdgeLength <= 0d ||
            !_world.TryGetProvince(currentId, out var current) ||
            !_world.TryGetProvince(targetId, out var target))
        {
            return 0d;
        }

        var deltaX = current.CapitalPosition.X - target.CapitalPosition.X;
        var deltaY = current.CapitalPosition.Y - target.CapitalPosition.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) / _maximumEdgeLength;
    }

    private static double FindMaximumEdgeLength(GameWorld world)
    {
        var maximumLength = 0d;
        foreach (var province in world.Provinces.Values)
        {
            foreach (var neighborId in world.ProvinceGraph.GetNeighbors(province.Id))
            {
                if (!world.TryGetProvince(neighborId, out var neighbor))
                {
                    continue;
                }

                var deltaX = province.CapitalPosition.X - neighbor.CapitalPosition.X;
                var deltaY = province.CapitalPosition.Y - neighbor.CapitalPosition.Y;
                maximumLength = Math.Max(maximumLength, Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)));
            }
        }

        return maximumLength;
    }

    private static IReadOnlyList<ProvinceId> ReconstructPath(
        ProvinceId start,
        ProvinceId target,
        IReadOnlyDictionary<ProvinceId, ProvinceId> previous)
    {
        var path = new List<ProvinceId> { target };
        var current = target;
        while (current != start)
        {
            if (!previous.TryGetValue(current, out current))
            {
                return Array.Empty<ProvinceId>();
            }

            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
