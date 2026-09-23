namespace AOH.Game.Domain.Diplomacy;

public readonly record struct WarId(int Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
