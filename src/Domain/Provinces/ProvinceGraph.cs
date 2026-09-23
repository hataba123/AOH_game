namespace AOH.Game.Domain.Provinces;

public sealed class ProvinceGraph
{
    private readonly IReadOnlyDictionary<ProvinceId, ProvinceId[]> _adjacency;

    public ProvinceGraph(IReadOnlyDictionary<ProvinceId, ProvinceId[]> adjacency)
    {
        _adjacency = adjacency;
    }

    public IReadOnlyList<ProvinceId> GetNeighbors(ProvinceId provinceId)
    {
        return _adjacency.TryGetValue(provinceId, out var neighbors)
            ? neighbors
            : Array.Empty<ProvinceId>();
    }

    public bool AreNeighbors(ProvinceId first, ProvinceId second)
    {
        if (!_adjacency.TryGetValue(first, out var neighbors))
        {
            return false;
        }

        return Array.IndexOf(neighbors, second) >= 0;
    }
}
