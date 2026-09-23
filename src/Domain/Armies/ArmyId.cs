namespace AOH.Game.Domain.Armies;

public readonly record struct ArmyId(int Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
