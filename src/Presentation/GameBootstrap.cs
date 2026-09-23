using AOH.Game.Domain;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;
using AOH.Game.Infrastructure.Persistence;
using AOH.Game.Map;
using AOH.Game.Core;
using AOH.Game.Simulation;
using AOH.Game.Simulation.Economy;
using AOH.Game.Simulation.Population;
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

    [Signal]
    public delegate void WorldReadyEventHandler(Godot.Collections.Dictionary summary);

    [Signal]
    public delegate void ProvinceSelectedEventHandler(int provinceId);

    [Signal]
    public delegate void ProvinceHoveredEventHandler(int provinceId);

    [Signal]
    public delegate void WorldTickedEventHandler(Godot.Collections.Dictionary summary);

    public override void _Ready()
    {
        try
        {
            var data = new JsonGameDataRepository().Load();
            _world = data.World;
            _simulationEngine = new SimulationEngine(
                _world,
                new GameTime(data.StartDate, data.StartingSpeed),
                new EconomySystem(),
                new PopulationSystem());
            _provinceMap = new ProvinceMap();
            _provinceMap.Configure(data.World, data.ColorLookup, data.MapData);
            _provinceMap.ProvinceSelectionChanged += HandleProvinceSelected;
            _provinceMap.ProvinceHoverChanged += HandleProvinceHovered;
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

        foreach (var country in _world.Countries.Values.OrderBy(country => country.Id.Value))
        {
            result.Add(new Godot.Collections.Dictionary
            {
                ["countryId"] = country.Id.Value,
                ["name"] = country.Name,
                ["mapColor"] = country.MapColor,
                ["capitalProvinceId"] = country.CapitalProvinceId,
                ["provinceCount"] = _world.Provinces.Values.Count(province => province.OwnerCountryId == country.Id),
                ["isAiControlled"] = country.IsAiControlled,
                ["treasury"] = country.Treasury,
                ["income"] = country.Income,
                ["expenses"] = country.Expenses,
                ["population"] = country.Population,
                ["manpower"] = country.Manpower
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
    }

    private Godot.Collections.Dictionary CreateWorldSummary()
    {
        return new Godot.Collections.Dictionary
        {
            ["provinceCount"] = _world?.Provinces.Count ?? 0,
            ["countryCount"] = _world?.Countries.Count ?? 0,
            ["date"] = _simulationEngine?.Time.CurrentDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            ["speed"] = _simulationEngine is null ? 0 : (int)_simulationEngine.Time.Speed,
            ["tickCount"] = _simulationEngine?.Time.TickCount ?? 0,
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
}
