using AOH.Game.Core;
using AOH.Game.Domain;
using AOH.Game.Domain.Armies;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Diplomacy;
using AOH.Game.Domain.Provinces;
using AOH.Game.Simulation.AI;

namespace AOH.Game.Infrastructure.Persistence;

public sealed class GameSaveData
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public DateTimeOffset SavedAtUtc { get; init; }

    public DateOnly CurrentDate { get; init; }

    public GameSpeed Speed { get; init; }

    public long TickCount { get; init; }

    public int RandomSeed { get; init; }

    public uint RandomState { get; init; }

    public int AiDaysUntilDecision { get; init; }

    public List<CountrySaveData> Countries { get; init; } = [];

    public List<ProvinceSaveData> Provinces { get; init; } = [];

    public List<ArmySaveData> Armies { get; init; } = [];

    public List<WarSaveData> Wars { get; init; } = [];

    public static GameSaveData Capture(GameWorld world, GameTime gameTime, GameRandom random, AiSystem aiSystem)
    {
        return new GameSaveData
        {
            SavedAtUtc = DateTimeOffset.UtcNow,
            CurrentDate = gameTime.CurrentDate,
            Speed = gameTime.Speed,
            TickCount = gameTime.TickCount,
            RandomSeed = random.Seed,
            RandomState = random.State,
            AiDaysUntilDecision = aiSystem.DaysUntilDecision,
            Countries = world.Countries.Values.Select(country => new CountrySaveData
            {
                CountryId = country.Id.Value,
                Treasury = country.Treasury,
                Income = country.Income,
                Expenses = country.Expenses
            }).ToList(),
            Provinces = world.Provinces.Values.Select(province => new ProvinceSaveData
            {
                ProvinceId = province.Id.Value,
                OwnerCountryId = province.OwnerCountryId.Value,
                ControllerCountryId = province.ControllerCountryId.Value,
                Population = province.Population,
                Manpower = province.Manpower
            }).ToList(),
            Armies = world.Armies.Values.Select(army => new ArmySaveData
            {
                ArmyId = army.Id.Value,
                OwnerCountryId = army.OwnerCountryId.Value,
                CurrentProvinceId = army.CurrentProvinceId.Value,
                TargetProvinceId = army.TargetProvinceId?.Value,
                Path = army.Path.Select(provinceId => provinceId.Value).ToArray(),
                Soldiers = army.Soldiers,
                Morale = army.Morale,
                Organization = army.Organization,
                MovementProgress = army.MovementProgress,
                Attack = army.Attack,
                Defense = army.Defense
            }).ToList(),
            Wars = world.Wars.Values.Where(war => war.IsActive).Select(war => new WarSaveData
            {
                WarId = war.Id.Value,
                AttackerCountryId = war.AttackerIds.Single().Value,
                DefenderCountryId = war.DefenderIds.Single().Value,
                StartDate = war.StartDate,
                AttackerWarScore = war.AttackerWarScore
            }).ToList()
        };
    }

    public IReadOnlyList<string> Validate(GameWorld world)
    {
        var errors = new List<string>();
        if (FormatVersion != CurrentFormatVersion)
        {
            errors.Add($"Phiên bản tệp lưu {FormatVersion} không được hỗ trợ.");
        }

        if (!Enum.IsDefined(Speed) || TickCount < 0 || RandomState == 0 || AiDaysUntilDecision is < 0 or >= 7)
        {
            errors.Add("Trạng thái thời gian, seed hoặc lịch AI không hợp lệ.");
        }

        if (Countries is null || Provinces is null || Armies is null || Wars is null)
        {
            errors.Add("Tệp lưu thiếu một hoặc nhiều danh sách dữ liệu.");
            return errors;
        }

        if (Countries.Any(country => country is null) || Provinces.Any(province => province is null) ||
            Armies.Any(army => army is null) || Wars.Any(war => war is null))
        {
            errors.Add("Tệp lưu chứa một mục dữ liệu rỗng.");
            return errors;
        }

        ValidateIds(Countries, country => country.CountryId, world.Countries.Keys.Select(id => id.Value), "quốc gia", errors);
        ValidateIds(Provinces, province => province.ProvinceId, world.Provinces.Keys.Select(id => id.Value), "tỉnh", errors);

        var countryIds = world.Countries.Keys.Select(id => id.Value).ToHashSet();
        foreach (var country in Countries)
        {
            if (!double.IsFinite(country.Treasury) || country.Treasury < 0d ||
                !double.IsFinite(country.Income) || country.Income < 0d ||
                !double.IsFinite(country.Expenses) || country.Expenses < 0d)
            {
                errors.Add($"Ngân sách của quốc gia {country.CountryId} không hợp lệ.");
            }
        }

        foreach (var province in Provinces)
        {
            if (!countryIds.Contains(province.OwnerCountryId) || !countryIds.Contains(province.ControllerCountryId) ||
                province.Population < 0 || province.Manpower < 0)
            {
                errors.Add($"Trạng thái tỉnh {province.ProvinceId} không hợp lệ.");
            }
        }

        var warIds = new HashSet<int>();
        var warPairs = new HashSet<(int First, int Second)>();
        foreach (var war in Wars)
        {
            var pair = war.AttackerCountryId < war.DefenderCountryId
                ? (war.AttackerCountryId, war.DefenderCountryId)
                : (war.DefenderCountryId, war.AttackerCountryId);
            if (war.WarId <= 0 || !warIds.Add(war.WarId) || !countryIds.Contains(war.AttackerCountryId) ||
                !countryIds.Contains(war.DefenderCountryId) || war.AttackerCountryId == war.DefenderCountryId ||
                !warPairs.Add(pair) || !double.IsFinite(war.AttackerWarScore) || war.AttackerWarScore is < -100d or > 100d)
            {
                errors.Add($"Dữ liệu chiến tranh {war.WarId} không hợp lệ.");
            }
        }

        var armyIds = new HashSet<int>();
        var provinceById = Provinces
            .GroupBy(province => province.ProvinceId)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (var army in Armies)
        {
            if (army.ArmyId <= 0 || !armyIds.Add(army.ArmyId) || !countryIds.Contains(army.OwnerCountryId) ||
                !provinceById.ContainsKey(army.CurrentProvinceId) || army.Soldiers <= 0 ||
                !double.IsFinite(army.Morale) || army.Morale is < 0d or > 1d ||
                !double.IsFinite(army.Organization) || army.Organization is < 0d or > 1d ||
                !double.IsFinite(army.MovementProgress) || army.MovementProgress < 0d ||
                !double.IsFinite(army.Attack) || army.Attack < 0d ||
                !double.IsFinite(army.Defense) || army.Defense < 0d || army.Path is null)
            {
                errors.Add($"Dữ liệu quân đội {army.ArmyId} không hợp lệ.");
                continue;
            }

            var expectedTargetId = army.Path.Length == 0 ? (int?)null : army.Path[^1];
            if (army.TargetProvinceId != expectedTargetId)
            {
                errors.Add($"Đường hành quân của quân đội {army.ArmyId} không khớp với tỉnh đích.");
                continue;
            }

            if (army.Path.Length == 0 && army.MovementProgress != 0d)
            {
                errors.Add($"Quân đội {army.ArmyId} có tiến độ di chuyển nhưng không có đường đi.");
            }

            var previousProvinceId = army.CurrentProvinceId;
            foreach (var nextProvinceId in army.Path)
            {
                if (!provinceById.TryGetValue(nextProvinceId, out var nextProvince) ||
                    !world.ProvinceGraph.AreNeighbors(new ProvinceId(previousProvinceId), new ProvinceId(nextProvinceId)))
                {
                    errors.Add($"Đường hành quân của quân đội {army.ArmyId} có kết nối không hợp lệ.");
                    break;
                }

                previousProvinceId = nextProvinceId;
            }
        }

        return errors;
    }

    public void Restore(GameWorld world, GameTime gameTime, GameRandom random, AiSystem aiSystem)
    {
        var errors = Validate(world);
        if (errors.Count > 0)
        {
            throw new InvalidDataException("Tệp lưu không hợp lệ:\n- " + string.Join("\n- ", errors));
        }

        foreach (var countrySave in Countries)
        {
            world.Countries[new CountryId(countrySave.CountryId)].RestoreEconomy(
                countrySave.Treasury,
                countrySave.Income,
                countrySave.Expenses);
        }

        foreach (var provinceSave in Provinces)
        {
            world.Provinces[new ProvinceId(provinceSave.ProvinceId)].RestoreState(
                new CountryId(provinceSave.OwnerCountryId),
                new CountryId(provinceSave.ControllerCountryId),
                provinceSave.Population,
                provinceSave.Manpower);
        }

        world.ClearRuntimeEntities();
        foreach (var warSave in Wars)
        {
            var war = new War(
                new WarId(warSave.WarId),
                new CountryId(warSave.AttackerCountryId),
                new CountryId(warSave.DefenderCountryId),
                warSave.StartDate);
            war.RestoreWarScore(warSave.AttackerWarScore);
            if (!world.TryAddWar(war))
            {
                throw new InvalidDataException($"Không thể khôi phục chiến tranh {warSave.WarId}.");
            }
        }

        foreach (var armySave in Armies)
        {
            var army = new Army(
                new ArmyId(armySave.ArmyId),
                new CountryId(armySave.OwnerCountryId),
                new ProvinceId(armySave.CurrentProvinceId),
                armySave.Soldiers,
                armySave.Morale,
                armySave.Organization,
                armySave.Attack,
                armySave.Defense);
            if (armySave.Path.Length > 0)
            {
                army.OrderMovement(new ProvinceId(armySave.TargetProvinceId!.Value),
                    armySave.Path.Select(provinceId => new ProvinceId(provinceId)).ToArray());
                army.RestoreMovementProgress(armySave.MovementProgress);
            }

            if (!world.TryAddArmy(army))
            {
                throw new InvalidDataException($"Không thể khôi phục quân đội {armySave.ArmyId}.");
            }
        }

        world.RecalculateDemographics();
        gameTime.RestoreState(CurrentDate, Speed, TickCount);
        random.RestoreState(RandomSeed, RandomState);
        aiSystem.RestoreSchedule(AiDaysUntilDecision);
    }

    private static void ValidateIds<T>(
        IReadOnlyCollection<T> items,
        Func<T, int> getId,
        IEnumerable<int> expectedIds,
        string itemName,
        ICollection<string> errors)
    {
        var ids = items.Select(getId).ToArray();
        if (ids.Length != ids.Distinct().Count())
        {
            errors.Add($"Danh sách {itemName} có ID bị trùng.");
        }

        var expected = expectedIds.ToHashSet();
        if (!ids.ToHashSet().SetEquals(expected))
        {
            errors.Add($"Danh sách {itemName} không khớp với dữ liệu thế giới.");
        }
    }
}

public sealed class CountrySaveData
{
    public int CountryId { get; init; }

    public double Treasury { get; init; }

    public double Income { get; init; }

    public double Expenses { get; init; }
}

public sealed class ProvinceSaveData
{
    public int ProvinceId { get; init; }

    public int OwnerCountryId { get; init; }

    public int ControllerCountryId { get; init; }

    public int Population { get; init; }

    public int Manpower { get; init; }
}

public sealed class ArmySaveData
{
    public int ArmyId { get; init; }

    public int OwnerCountryId { get; init; }

    public int CurrentProvinceId { get; init; }

    public int? TargetProvinceId { get; init; }

    public int[] Path { get; init; } = [];

    public int Soldiers { get; init; }

    public double Morale { get; init; }

    public double Organization { get; init; }

    public double MovementProgress { get; init; }

    public double Attack { get; init; }

    public double Defense { get; init; }
}

public sealed class WarSaveData
{
    public int WarId { get; init; }

    public int AttackerCountryId { get; init; }

    public int DefenderCountryId { get; init; }

    public DateOnly StartDate { get; init; }

    public double AttackerWarScore { get; init; }
}
