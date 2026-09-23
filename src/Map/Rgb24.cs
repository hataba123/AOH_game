namespace AOH.Game.Map;

public readonly record struct Rgb24(byte Red, byte Green, byte Blue)
{
    public uint PackedValue => ((uint)Red << 16) | ((uint)Green << 8) | Blue;

    public static Rgb24 FromProvinceId(int provinceId)
    {
        if (provinceId is <= 0 or > 0xFFFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(provinceId), "Province ID phải nằm trong khoảng 1 đến 16.777.215.");
        }

        return new Rgb24((byte)(provinceId >> 16), (byte)(provinceId >> 8), (byte)provinceId);
    }
}
