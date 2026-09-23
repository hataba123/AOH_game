using AOH.Game.Core;
using AOH.Game.Domain;

namespace AOH.Game.Simulation;

public interface IGameSystem
{
    void Process(GameWorld world);
}

public sealed class SimulationEngine
{
    private const int MaximumTicksPerFrame = 100;
    private readonly GameWorld _world;
    private readonly IGameSystem[] _systems;
    private double _accumulatedSeconds;

    public SimulationEngine(GameWorld world, GameTime gameTime, params IGameSystem[] systems)
    {
        _world = world;
        Time = gameTime;
        _systems = systems;
    }

    public GameTime Time { get; }

    public void ResetElapsedTime() => _accumulatedSeconds = 0d;

    public int AdvanceFrame(double elapsedSeconds)
    {
        if (elapsedSeconds < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        var ticksPerSecond = (int)Time.Speed;
        if (ticksPerSecond == 0)
        {
            return 0;
        }

        _accumulatedSeconds += elapsedSeconds;
        var secondsPerTick = 1d / ticksPerSecond;
        var ticksAdvanced = 0;

        while (_accumulatedSeconds >= secondsPerTick && ticksAdvanced < MaximumTicksPerFrame)
        {
            _accumulatedSeconds -= secondsPerTick;
            AdvanceTick();
            ticksAdvanced++;
        }

        return ticksAdvanced;
    }

    public void AdvanceTick()
    {
        foreach (var system in _systems)
        {
            system.Process(_world);
        }

        Time.AdvanceOneDay();
    }
}
