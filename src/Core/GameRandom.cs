namespace AOH.Game.Core;

public sealed class GameRandom
{
    private uint _state;

    public GameRandom(int seed)
    {
        Seed = seed;
        _state = unchecked((uint)seed);
        if (_state == 0)
        {
            _state = 0xA341316Cu;
        }
    }

    public int Seed { get; }

    public uint State => _state;

    public double NextDouble()
    {
        var value = _state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        _state = value;
        return value / ((double)uint.MaxValue + 1d);
    }

    public double NextDouble(double minimum, double maximum)
    {
        if (maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        return minimum + (NextDouble() * (maximum - minimum));
    }
}
