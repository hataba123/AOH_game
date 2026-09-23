using AOH.Game.Core;
using AOH.Game.Domain;
using AOH.Game.Domain.Provinces;
using AOH.Game.Simulation;
using Xunit;

namespace AOH.Tests;

public class GameTimeTests
{
    [Theory]
    [InlineData(GameSpeed.Speed1, 1)]
    [InlineData(GameSpeed.Speed2, 2)]
    [InlineData(GameSpeed.Speed3, 3)]
    [InlineData(GameSpeed.Speed4, 4)]
    [InlineData(GameSpeed.Speed5, 5)]
    public void AdvanceFrame_AdvancesConfiguredDaysPerSecond(GameSpeed speed, int expectedDays)
    {
        var world = new GameWorld([], [], new ProvinceGraph(new Dictionary<ProvinceId, ProvinceId[]>()));
        var gameTime = new GameTime(new DateOnly(1444, 1, 1), speed);
        var engine = new SimulationEngine(world, gameTime);

        var ticksAdvanced = engine.AdvanceFrame(1d);

        Assert.Equal(expectedDays, ticksAdvanced);
        Assert.Equal(new DateOnly(1444, 1, 1).AddDays(expectedDays), gameTime.CurrentDate);
        Assert.Equal(expectedDays, gameTime.TickCount);
    }

    [Fact]
    public void AdvanceFrame_DoesNotAdvanceWhenPaused()
    {
        var world = new GameWorld([], [], new ProvinceGraph(new Dictionary<ProvinceId, ProvinceId[]>()));
        var gameTime = new GameTime(new DateOnly(1444, 1, 1), GameSpeed.Paused);
        var engine = new SimulationEngine(world, gameTime);

        var ticksAdvanced = engine.AdvanceFrame(10d);

        Assert.Equal(0, ticksAdvanced);
        Assert.Equal(new DateOnly(1444, 1, 1), gameTime.CurrentDate);
        Assert.Equal(0, gameTime.TickCount);
    }
}
