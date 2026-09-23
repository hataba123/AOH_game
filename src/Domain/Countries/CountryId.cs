namespace AOH.Game.Domain.Countries;

public readonly record struct CountryId(int Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
