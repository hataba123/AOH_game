namespace AOH.Game.Domain.Provinces;

public readonly record struct ProvinceId(int Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
