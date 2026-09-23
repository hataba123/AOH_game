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

    public int Seed { get; private set; }

    public uint State => _state;

    public void RestoreState(uint state)
    {
        if (state == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        _state = state;
    }

    public void RestoreState(int seed, uint state)
    {
        RestoreState(state);
        Seed = seed;
    }

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
