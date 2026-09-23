using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;

namespace AOH.Game.Domain.Armies;

public sealed class Army
{
    private readonly List<ProvinceId> _path = [];

    public Army(
        ArmyId id,
        CountryId ownerCountryId,
        ProvinceId currentProvinceId,
        int soldiers,
        double morale = 1d,
        double organization = 1d,
        double attack = 1d,
        double defense = 1d)
    {
        if (soldiers <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(soldiers));
        }

        Id = id;
        OwnerCountryId = ownerCountryId;
        CurrentProvinceId = currentProvinceId;
        Soldiers = soldiers;
        Morale = morale;
        Organization = organization;
        Attack = attack;
        Defense = defense;
    }

    public ArmyId Id { get; }

    public CountryId OwnerCountryId { get; }

    public ProvinceId CurrentProvinceId { get; private set; }

    public ProvinceId? TargetProvinceId { get; private set; }

    public IReadOnlyList<ProvinceId> Path => _path;

    public int Soldiers { get; private set; }

    public double Morale { get; private set; }

    public double Organization { get; private set; }

    public double MovementProgress { get; private set; }

    public double Attack { get; }

    public double Defense { get; }

    public void OrderMovement(ProvinceId targetProvinceId, IReadOnlyList<ProvinceId> remainingPath)
    {
        if (remainingPath.Count == 0 || remainingPath[^1] != targetProvinceId)
        {
            throw new ArgumentException("Đường đi phải kết thúc tại tỉnh mục tiêu.", nameof(remainingPath));
        }

        _path.Clear();
        _path.AddRange(remainingPath);
        TargetProvinceId = targetProvinceId;
        MovementProgress = 0d;
    }

    internal void CancelMovement()
    {
        _path.Clear();
        TargetProvinceId = null;
        MovementProgress = 0d;
    }

    internal void AddMovementProgress(double amount)
    {
        if (amount > 0d && _path.Count > 0)
        {
            MovementProgress += amount;
        }
    }

    internal bool MoveToNextProvince(ProvinceId provinceId, double movementCost)
    {
        if (_path.Count == 0 || _path[0] != provinceId || MovementProgress < movementCost)
        {
            return false;
        }

        CurrentProvinceId = provinceId;
        MovementProgress -= movementCost;
        _path.RemoveAt(0);
        if (_path.Count == 0)
        {
            TargetProvinceId = null;
            MovementProgress = 0d;
        }

        return true;
    }

    internal int ApplyCasualties(double lossRatio)
    {
        if (Soldiers <= 0 || lossRatio <= 0d)
        {
            return 0;
        }

        var casualties = Math.Clamp((int)Math.Ceiling(Soldiers * lossRatio), 1, Soldiers);
        Soldiers -= casualties;
        Morale = Math.Clamp(Morale - 0.02d, 0.1d, 1d);
        Organization = Math.Clamp(Organization - 0.04d, 0.1d, 1d);
        return casualties;
    }
}
