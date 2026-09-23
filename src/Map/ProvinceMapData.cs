namespace AOH.Game.Map;

public sealed class ProvinceMapData
{
    public ProvinceMapData(int width, int height, byte[] rgbPixels, Rgb24 seaColor)
    {
        Width = width;
        Height = height;
        RgbPixels = rgbPixels;
        SeaColor = seaColor;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] RgbPixels { get; }

    public Rgb24 SeaColor { get; }

    public bool TryGetProvinceId(int x, int y, ProvinceColorLookup lookup, out AOH.Game.Domain.Provinces.ProvinceId provinceId)
    {
        provinceId = default;
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return false;
        }

        var pixelIndex = (y * Width + x) * 3;
        var color = new Rgb24(RgbPixels[pixelIndex], RgbPixels[pixelIndex + 1], RgbPixels[pixelIndex + 2]);
        return color != SeaColor && lookup.TryGetProvinceId(color, out provinceId);
    }
}
