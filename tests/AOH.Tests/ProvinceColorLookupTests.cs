using AOH.Game.Domain.Provinces;
using AOH.Game.Map;
using Xunit;

namespace AOH.Tests;

public class ProvinceColorLookupTests
{
    [Fact]
    public void Lookup_CanMapBidirectionally_ForDemoProvinces()
    {
        var provinceIds = Enumerable.Range(1, 20).Select(id => new ProvinceId(id)).ToArray();
        var lookup = new ProvinceColorLookup(provinceIds);

        Assert.Equal(20, lookup.Count);

        foreach (var id in provinceIds)
        {
            // ProvinceId -> Color
            Assert.True(lookup.TryGetColor(id, out var color));

            // Color -> ProvinceId
            Assert.True(lookup.TryGetProvinceId(color, out var resolvedId));
            Assert.Equal(id, resolvedId);
        }
    }

    [Fact]
    public void Lookup_ReturnsFalse_ForUnregisteredColor()
    {
        var provinceIds = new[] { new ProvinceId(1), new ProvinceId(2) };
        var lookup = new ProvinceColorLookup(provinceIds);

        var unknownColor = new Rgb24(255, 255, 255);
        Assert.False(lookup.TryGetProvinceId(unknownColor, out _));
    }

    [Fact]
    public void Lookup_ThrowsException_OnDuplicateProvinceId()
    {
        var provinceIds = new[] { new ProvinceId(1), new ProvinceId(1) };
        Assert.Throws<InvalidDataException>(() => new ProvinceColorLookup(provinceIds));
    }
}
