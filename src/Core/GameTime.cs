namespace AOH.Game.Core;

public sealed class GameTime
{
    public GameTime(DateOnly currentDate, GameSpeed speed = GameSpeed.Speed1)
    {
        if (!Enum.IsDefined(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }

        CurrentDate = currentDate;
        Speed = speed;
    }

    public DateOnly CurrentDate { get; private set; }

    public GameSpeed Speed { get; private set; }

    public long TickCount { get; private set; }

    public void SetSpeed(GameSpeed speed)
    {
        if (!Enum.IsDefined(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }

        Speed = speed;
    }

    public void RestoreState(DateOnly currentDate, GameSpeed speed, long tickCount)
    {
        if (!Enum.IsDefined(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }

        if (tickCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickCount));
        }

        CurrentDate = currentDate;
        Speed = speed;
        TickCount = tickCount;
    }

    internal void AdvanceOneDay()
    {
        CurrentDate = CurrentDate.AddDays(1);
        TickCount++;
    }
}
