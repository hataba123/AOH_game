using AOH.Game.Domain.Provinces;
using Xunit;

namespace AOH.Tests;

public class ProvinceGraphTests
{
    [Fact]
    public void GetNeighbors_ReturnsCorrectNeighbors()
    {
        var adjacency = new Dictionary<ProvinceId, ProvinceId[]>
        {
            [new ProvinceId(1)] = [new ProvinceId(2), new ProvinceId(3)],
            [new ProvinceId(2)] = [new ProvinceId(1)],
            [new ProvinceId(3)] = [new ProvinceId(1)]
        };

        var graph = new ProvinceGraph(adjacency);
        var neighbors = graph.GetNeighbors(new ProvinceId(1));

        Assert.Equal(2, neighbors.Count);
        Assert.Contains(new ProvinceId(2), neighbors);
        Assert.Contains(new ProvinceId(3), neighbors);
    }

    [Fact]
    public void AreNeighbors_ReturnsTrue_OnlyForConnectedNodes()
    {
        var adjacency = new Dictionary<ProvinceId, ProvinceId[]>
        {
            [new ProvinceId(1)] = [new ProvinceId(2)],
            [new ProvinceId(2)] = [new ProvinceId(1), new ProvinceId(3)],
            [new ProvinceId(3)] = [new ProvinceId(2)]
        };

        var graph = new ProvinceGraph(adjacency);

        Assert.True(graph.AreNeighbors(new ProvinceId(1), new ProvinceId(2)));
        Assert.True(graph.AreNeighbors(new ProvinceId(2), new ProvinceId(1)));
        Assert.False(graph.AreNeighbors(new ProvinceId(1), new ProvinceId(3)));
        Assert.False(graph.AreNeighbors(new ProvinceId(3), new ProvinceId(1)));
    }

    [Fact]
    public void GetNeighbors_ReturnsEmpty_ForUnknownProvince()
    {
        var graph = new ProvinceGraph(new Dictionary<ProvinceId, ProvinceId[]>());
        var neighbors = graph.GetNeighbors(new ProvinceId(999));

        Assert.Empty(neighbors);
    }
}
