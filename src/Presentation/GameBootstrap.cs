using AOH.Game.Domain;
using AOH.Game.Domain.Armies;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Diplomacy;
using AOH.Game.Domain.Provinces;
using AOH.Game.Infrastructure.Persistence;
using AOH.Game.Map;
using AOH.Game.Core;
using AOH.Game.Simulation;
using AOH.Game.Simulation.Economy;
using AOH.Game.Simulation.Population;
using AOH.Game.Simulation.Military;
using AOH.Game.Simulation.AI;
using System.IO;
using Godot;

namespace AOH.Game.Presentation;

[GlobalClass]
public partial class GameBootstrap : Node2D
{
    private const string HudScenePath = "res://Scenes/UI/GameHud.tscn";
    private GameWorld? _world;
    private ProvinceMap? _provinceMap;
    private Node? _hud;
    private SimulationEngine? _simulationEngine;
    private ProvincePathfinder? _pathfinder;
    private GameTime? _gameTime;
    private GameRandom? _gameRandom;
    private AiSystem? _aiSystem;
    private JsonSaveGameRepository? _saveRepository;
    private readonly ArmyRecruitmentService _armyRecruitmentService = new();

    public event Action? ReturnRequested;

    [Signal]
    public delegate void WorldReadyEventHandler(Godot.Collections.Dictionary summary);

    [Signal]
    public delegate void ProvinceSelectedEventHandler(int provinceId);

    [Signal]
    public delegate void ProvinceHoveredEventHandler(int provinceId);

    [Signal]
    public delegate void WorldTickedEventHandler(Godot.Collections.Dictionary summary);

    [Signal]
    public delegate void ArmySelectedEventHandler(int armyId);

    public override void _Ready()
    {
        try
        {
            var data = new JsonGameDataRepository().Load();
            _world = data.World;
            _pathfinder = new ProvincePathfinder(_world);
            _gameTime = new GameTime(data.StartDate, data.StartingSpeed);
            _gameRandom = new GameRandom(data.RandomSeed);
            _aiSystem = new AiSystem(_gameTime, _gameRandom);
            _saveRepository = new JsonSaveGameRepository(Path.Combine(OS.GetUserDataDir(), "saves"));
            _simulationEngine = new SimulationEngine(
                _world,
                _gameTime,
                new EconomySystem(),
                new PopulationSystem(),
                new ArmyMovementSystem(),
                new CombatSystem(_gameRandom),
                _aiSystem);
            _provinceMap = new ProvinceMap();
            _provinceMap.Configure(data.World, data.ColorLookup, data.MapData);
            _provinceMap.ProvinceSelectionChanged += HandleProvinceSelected;
            _provinceMap.ProvinceHoverChanged += HandleProvinceHovered;
            _provinceMap.ArmySelectionChanged += HandleArmySelected;
            AddChild(_provinceMap);

            LoadHud();
            EmitSignal(SignalName.WorldReady, CreateWorldSummary());
            GD.Print($"Đã tải bản đồ Lục Hải: {_world.Provinces.Count} tỉnh, {_world.Countries.Count} quốc gia.");
        }
        catch (Exception exception)
        {
            GD.PushError($"Không thể khởi tạo game: {exception.Message}");
        }
    }

    public override void _Process(double delta)
    {
        if (_simulationEngine?.AdvanceFrame(delta) > 0)
        {
            _provinceMap?.QueueRedraw();
            EmitSignal(SignalName.WorldTicked, CreateWorldSummary());
        }
    }

    public void SetGameSpeed(int speed)
    {
        if (_simulationEngine is null || !Enum.IsDefined((GameSpeed)speed))
        {
            return;
        }

        _simulationEngine.Time.SetSpeed((GameSpeed)speed);
        EmitSignal(SignalName.WorldTicked, CreateWorldSummary());
    }

    public Godot.Collections.Dictionary SaveGame(string slotName)
    {
        if (_world is null || _gameTime is null || _gameRandom is null || _aiSystem is null || _saveRepository is null)
        {
            return CreateCommandResult(false, "Chưa thể lưu vì game chưa khởi tạo xong.");
        }

        try
        {
            _saveRepository.Save(slotName, GameSaveData.Capture(_world, _gameTime, _gameRandom, _aiSystem));
            return CreateCommandResult(true, $"Đã lưu game tại ô '{slotName}'.");
        }
        catch (Exception exception)
        {
            GD.PushError($"Lưu game thất bại: {exception.Message}");
            return CreateCommandResult(false, $"Lưu game thất bại: {exception.Message}");
        }
    }

    public Godot.Collections.Dictionary LoadGame(string slotName)
    {
        if (_world is null || _gameTime is null || _gameRandom is null || _aiSystem is null ||
            _simulationEngine is null || _saveRepository is null)
        {
            return CreateCommandResult(false, "Chưa thể tải vì game chưa khởi tạo xong.");
        }

        try
        {
            var saveData = _saveRepository.Load(slotName);
            saveData.Restore(_world, _gameTime, _gameRandom, _aiSystem);
            _simulationEngine.ResetElapsedTime();
            _provinceMap?.QueueRedraw();
            EmitSignal(SignalName.WorldTicked, CreateWorldSummary());
            return CreateCommandResult(true, $"Đã tải bản lưu '{slotName}'.");
        }
        catch (Exception exception)
        {
            GD.PushError($"Tải game thất bại: {exception.Message}");
            return CreateCommandResult(false, $"Tải game thất bại: {exception.Message}");
        }
    }

    public bool SaveExists(string slotName)
    {
        try
        {
            return _saveRepository?.SaveExists(slotName) ?? false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public void ExitToMenu() => ReturnRequested?.Invoke();

    public Godot.Collections.Dictionary GetGameSummary() => CreateWorldSummary();

    public Godot.Collections.Dictionary RecruitArmy(int provinceId, int soldiers)
    {
        if (_world is null)
        {
            return CreateCommandResult(false, "Thế giới chưa được tải.");
        }

        var playerCountry = _world.Countries.Values.FirstOrDefault(country => !country.IsAiControlled);
        if (playerCountry is null)
        {
            return CreateCommandResult(false, "Không tìm thấy quốc gia người chơi.");
        }

        var result = _armyRecruitmentService.Recruit(_world, new ProvinceId(provinceId), playerCountry.Id, soldiers);
        if (!result.Success || result.Army is null)
        {
            return CreateCommandResult(false, result.Message);
        }

        _provinceMap?.SelectArmy(result.Army.Id);
        _provinceMap?.QueueRedraw();
        var commandResult = CreateCommandResult(true, result.Message);
        commandResult["armyId"] = result.Army.Id.Value;
        EmitSignal(SignalName.WorldTicked, CreateWorldSummary());
        return commandResult;
    }

    public Godot.Collections.Dictionary MoveArmy(int armyId, int targetProvinceId)
    {
        if (_world is null || _pathfinder is null || !_world.Armies.TryGetValue(new ArmyId(armyId), out var army))
        {
            return CreateCommandResult(false, "Không tìm thấy đội quân.");
        }

        if (_world.Countries[army.OwnerCountryId].IsAiControlled)
        {
            return CreateCommandResult(false, "Bạn chỉ có thể điều khiển quân đội của quốc gia người chơi.");
        }

        var target = new ProvinceId(targetProvinceId);
        var path = _pathfinder.FindPath(army.CurrentProvinceId, target, army.OwnerCountryId);
        if (path.Count == 0)
        {
            return CreateCommandResult(false, "Không tìm thấy đường đi trong lãnh thổ đang kiểm soát.");
        }

        if (path.Count == 1)
        {
            return CreateCommandResult(false, "Quân đội đã ở tỉnh được chọn.");
        }

        army.OrderMovement(target, path.Skip(1).ToArray());
        _provinceMap?.QueueRedraw();
        EmitSignal(SignalName.WorldTicked, CreateWorldSummary());
        return CreateCommandResult(true, $"Đã phát lệnh di chuyển qua {path.Count - 1} tỉnh.");
    }

    public Godot.Collections.Dictionary DeclareWar(int targetCountryId)
    {
        if (_world is null || _simulationEngine is null || !_world.TryGetCountry(new CountryId(targetCountryId), out var target))
        {
            return CreateCommandResult(false, "Không tìm thấy quốc gia mục tiêu.");
        }

        var player = _world.Countries.Values.FirstOrDefault(country => !country.IsAiControlled);
        if (player is null || player.Id == target.Id || !target.IsAiControlled)
        {
            return CreateCommandResult(false, "Không thể tuyên chiến với quốc gia này.");
        }

        if (!_world.TryDeclareWar(player.Id, target.Id, _simulationEngine.Time.CurrentDate, out var war) || war is null)
        {
            return CreateCommandResult(false, "Hai quốc gia đã có chiến tranh hoặc dữ liệu không hợp lệ.");
        }

        EmitSignal(SignalName.WorldTicked, CreateWorldSummary());
        return CreateCommandResult(true, $"Đã tuyên chiến với {target.Name}.");
    }

    public Godot.Collections.Dictionary ConcludePeace(int targetCountryId)
    {
        if (_world is null)
        {
            return CreateCommandResult(false, "Thế giới chưa được tải.");
        }

        var player = _world.Countries.Values.FirstOrDefault(country => !country.IsAiControlled);
        var targetId = new CountryId(targetCountryId);
        if (player is null || !_world.TryGetActiveWar(player.Id, targetId, out var war))
        {
            return CreateCommandResult(false, "Không có chiến tranh đang diễn ra với quốc gia này.");
        }

        if (!_world.TryConcludePeace(war.Id, player.Id))
        {
            return CreateCommandResult(false, "Không thể ký hòa ước.");
        }

        _provinceMap?.QueueRedraw();
        EmitSignal(SignalName.WorldTicked, CreateWorldSummary());
        return CreateCommandResult(true, "Hòa ước đã được ký; các tỉnh bị chiếm đã được giải quyết theo điểm chiến tranh.");
    }

    public Godot.Collections.Dictionary GetArmyDetails(int armyId)
    {
        if (_world is null || !_world.Armies.TryGetValue(new ArmyId(armyId), out var army) ||
            !_world.TryGetProvince(army.CurrentProvinceId, out var currentProvince))
        {
            return new Godot.Collections.Dictionary();
        }

        var owner = _world.Countries[army.OwnerCountryId];
        var targetName = army.TargetProvinceId is { } targetId && _world.TryGetProvince(targetId, out var targetProvince)
            ? targetProvince.Name
            : string.Empty;
        return new Godot.Collections.Dictionary
        {
            ["armyId"] = army.Id.Value,
            ["ownerCountryId"] = army.OwnerCountryId.Value,
            ["ownerCountryName"] = owner.Name,
            ["isPlayerArmy"] = !owner.IsAiControlled,
            ["currentProvinceId"] = army.CurrentProvinceId.Value,
            ["currentProvinceName"] = currentProvince.Name,
            ["targetProvinceName"] = targetName,
            ["soldiers"] = army.Soldiers,
            ["pathLength"] = army.Path.Count,
            ["movementProgress"] = army.MovementProgress
        };
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> GetArmiesAtProvince(int provinceId)
    {
        var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (_world is null)
        {
            return result;
        }

        foreach (var army in _world.Armies.Values.Where(army => army.CurrentProvinceId.Value == provinceId))
        {
            var owner = _world.Countries[army.OwnerCountryId];
            result.Add(new Godot.Collections.Dictionary
            {
                ["armyId"] = army.Id.Value,
                ["ownerCountryName"] = owner.Name,
                ["isPlayerArmy"] = !owner.IsAiControlled,
                ["soldiers"] = army.Soldiers
            });
        }

        return result;
    }

    public Godot.Collections.Dictionary GetProvinceDetails(int provinceId)
    {
        if (_world is null || !_world.TryGetProvince(new ProvinceId(provinceId), out var province))
        {
            return new Godot.Collections.Dictionary();
        }

        var owner = _world.Countries[province.OwnerCountryId];
        var controller = _world.Countries[province.ControllerCountryId];
        return new Godot.Collections.Dictionary
        {
            ["provinceId"] = province.Id.Value,
            ["name"] = province.Name,
            ["ownerCountryId"] = owner.Id.Value,
            ["ownerCountryName"] = owner.Name,
            ["ownerMapColor"] = owner.MapColor,
            ["controllerCountryId"] = controller.Id.Value,
            ["controllerCountryName"] = controller.Name,
            ["population"] = province.Population,
            ["economy"] = province.Economy,
            ["development"] = province.Development,
            ["taxRate"] = province.TaxRate,
            ["manpower"] = province.Manpower,
            ["terrain"] = province.Terrain.ToString(),
            ["isCoastal"] = province.IsCoastal
        };
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> GetCountrySummaries()
    {
        var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (_world is null)
        {
            return result;
        }

        var provinceCounts = new Dictionary<CountryId, int>(_world.Countries.Count);
        foreach (var country in _world.Countries.Values)
        {
            provinceCounts.Add(country.Id, 0);
        }

        foreach (var province in _world.Provinces.Values)
        {
            provinceCounts[province.OwnerCountryId]++;
        }

        var playerCountry = _world.Countries.Values.FirstOrDefault(candidate => !candidate.IsAiControlled);
        foreach (var country in _world.Countries.Values.OrderBy(country => country.Id.Value))
        {
            War activeWar = null!;
            var hasActiveWar = playerCountry is not null &&
                _world.TryGetActiveWar(playerCountry.Id, country.Id, out activeWar);
            result.Add(new Godot.Collections.Dictionary
            {
                ["countryId"] = country.Id.Value,
                ["name"] = country.Name,
                ["mapColor"] = country.MapColor,
                ["capitalProvinceId"] = country.CapitalProvinceId,
                ["provinceCount"] = provinceCounts[country.Id],
                ["isAiControlled"] = country.IsAiControlled,
                ["treasury"] = country.Treasury,
                ["income"] = country.Income,
                ["expenses"] = country.Expenses,
                ["population"] = country.Population,
                ["manpower"] = country.Manpower,
                ["armyCount"] = _world.Armies.Values.Count(army => army.OwnerCountryId == country.Id),
                ["isAtWarWithPlayer"] = hasActiveWar,
                ["warScore"] = hasActiveWar && playerCountry is not null
                    ? activeWar.AttackerIds.Contains(playerCountry.Id) ? activeWar.AttackerWarScore : -activeWar.AttackerWarScore
                    : 0d,
                ["warStartDate"] = hasActiveWar ? activeWar.StartDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) : string.Empty
            });
        }

        return result;
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> GetProvinceNeighborSummaries(int provinceId)
    {
        var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (_world is null)
        {
            return result;
        }

        foreach (var neighborId in _world.ProvinceGraph.GetNeighbors(new ProvinceId(provinceId)))
        {
            if (!_world.TryGetProvince(neighborId, out var province))
            {
                continue;
            }

            var owner = _world.Countries[province.OwnerCountryId];
            result.Add(new Godot.Collections.Dictionary
            {
                ["provinceId"] = province.Id.Value,
                ["name"] = province.Name,
                ["ownerCountryName"] = owner.Name,
                ["ownerMapColor"] = owner.MapColor
            });
        }

        return result;
    }

    private void LoadHud()
    {
        if (!ResourceLoader.Exists(HudScenePath))
        {
            GD.Print($"Chưa có giao diện. Tạo scene tại {HudScenePath} để tự động kết nối HUD.");
            return;
        }

        var hudScene = GD.Load<PackedScene>(HudScenePath);
        if (hudScene is null)
        {
            GD.PushError($"Không thể tải giao diện: {HudScenePath}.");
            return;
        }

        _hud = hudScene.Instantiate();
        var hudLayer = new CanvasLayer { Layer = 10 };
        AddChild(hudLayer);
        hudLayer.AddChild(_hud);

        if (_hud.HasMethod("BindSession"))
        {
            _hud.Call("BindSession", this);
        }

        if (_hud.HasMethod("InitializeWorld"))
        {
            _hud.Call("InitializeWorld", CreateWorldSummary());
        }

        if (_hud.HasMethod("OnProvinceSelected"))
        {
            Connect(SignalName.ProvinceSelected, new Callable(_hud, "OnProvinceSelected"));
        }

        if (_hud.HasMethod("OnProvinceHovered"))
        {
            Connect(SignalName.ProvinceHovered, new Callable(_hud, "OnProvinceHovered"));
        }

        if (_hud.HasMethod("OnWorldTicked"))
        {
            Connect(SignalName.WorldTicked, new Callable(_hud, "OnWorldTicked"));
        }

        if (_hud.HasMethod("OnArmySelected"))
        {
            Connect(SignalName.ArmySelected, new Callable(_hud, "OnArmySelected"));
        }
    }

    private Godot.Collections.Dictionary CreateWorldSummary()
    {
        return new Godot.Collections.Dictionary
        {
            ["provinceCount"] = _world?.Provinces.Count ?? 0,
            ["countryCount"] = _world?.Countries.Count ?? 0,
            ["armyCount"] = _world?.Armies.Count ?? 0,
            ["warCount"] = _world?.Wars.Values.Count(war => war.IsActive) ?? 0,
            ["aiCountryCount"] = _world?.Countries.Values.Count(country => country.IsAiControlled) ?? 0,
            ["date"] = _simulationEngine?.Time.CurrentDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            ["speed"] = _simulationEngine is null ? 0 : (int)_simulationEngine.Time.Speed,
            ["tickCount"] = _simulationEngine?.Time.TickCount ?? 0,
            ["tickDurationMs"] = _simulationEngine?.LastTickDurationMilliseconds ?? 0d,
            ["averageTickDurationMs"] = _simulationEngine?.AverageTickDurationMilliseconds ?? 0d,
            ["playerTreasury"] = _world?.Countries.Values.FirstOrDefault(country => !country.IsAiControlled)?.Treasury ?? 0d,
            ["playerIncome"] = _world?.Countries.Values.FirstOrDefault(country => !country.IsAiControlled)?.Income ?? 0d
        };
    }

    private void HandleProvinceSelected(ProvinceId? provinceId)
    {
        EmitSignal(SignalName.ProvinceSelected, provinceId?.Value ?? -1);
    }

    private void HandleProvinceHovered(ProvinceId? provinceId)
    {
        EmitSignal(SignalName.ProvinceHovered, provinceId?.Value ?? -1);
    }

    private void HandleArmySelected(ArmyId? armyId)
    {
        EmitSignal(SignalName.ArmySelected, armyId?.Value ?? -1);
    }

    private static Godot.Collections.Dictionary CreateCommandResult(bool success, string message)
    {
        return new Godot.Collections.Dictionary
        {
            ["success"] = success,
            ["message"] = message
        };
    }
}
