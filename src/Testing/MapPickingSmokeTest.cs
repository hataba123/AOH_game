using AOH.Game.Domain.Provinces;
using AOH.Game.Infrastructure.Persistence;
using AOH.Game.Map;
using AOH.Game.Presentation;
using Godot;

namespace AOH.Game.Tests;

[GlobalClass]
public partial class MapPickingSmokeTest : Node
{
    private int _lastSelectedProvinceId = -1;

    public override async void _Ready()
    {
        try
        {
            var data = new JsonGameDataRepository().Load();
            ValidateColorsAndGraph(data);

            var map = new ProvinceMap();
            map.Configure(data.World, data.ColorLookup, data.MapData);
            map.ProvinceSelectionChanged += provinceId => _lastSelectedProvinceId = provinceId?.Value ?? -1;
            AddChild(map);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var camera = map.GetNode<Camera2D>("MapCamera");
            foreach (var zoom in new[] { 0.55f, 0.7f, 1.25f })
            {
                camera.Zoom = Vector2.One * zoom;
                camera.Position = new Vector2(515f, 480f);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                VerifyEveryProvinceCanBePicked(map, data);
            }

            var previousZoom = camera.Zoom.X;
            PushMouseButton(map, new Vector2(800f, 450f), MouseButton.WheelUp);
            Assert(camera.Zoom.X > previousZoom, "Mouse wheel should zoom the map in.");

            var previousPosition = camera.Position;
            PushMouseButton(map, new Vector2(800f, 450f), MouseButton.Right);
            map._UnhandledInput(new InputEventMouseMotion
            {
                Position = new Vector2(830f, 470f),
                Relative = new Vector2(30f, 20f),
                ButtonMask = MouseButtonMask.Right
            });
            PushMouseButton(map, new Vector2(830f, 470f), MouseButton.Right, pressed: false);
            Assert(camera.Position != previousPosition, "Dragging with the right mouse button should pan the map.");
            VerifyEveryProvinceCanBePicked(map, data);

            var game = new GameBootstrap();
            AddChild(game);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var recruitment = game.RecruitArmy(7, 1_000);
            Assert(recruitment["success"].AsBool(), $"Army recruitment failed: {recruitment["message"].AsString()}");
            var armyId = recruitment["armyId"].AsInt32();
            var movement = game.MoveArmy(armyId, 6);
            Assert(movement["success"].AsBool(), $"Army movement order failed: {movement["message"].AsString()}");
            game.SetGameSpeed(5);
            game._Process(1d);

            var army = game.GetArmyDetails(armyId);
            Assert(army["currentProvinceId"].AsInt32() == 6, "The army should complete movement to the neighboring province.");

            GD.Print("MapPickingSmokeTest passed: map picking, path-based recruitment, and army movement.");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"MapPickingSmokeTest failed: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private static void ValidateColorsAndGraph(GameDataLoadResult data)
    {
        foreach (var province in data.World.Provinces.Values)
        {
            Assert(data.ColorLookup.TryGetColor(province.Id, out var color), $"Province {province.Id} has no map color.");
            Assert(data.ColorLookup.TryGetProvinceId(color, out var decodedId) && decodedId == province.Id,
                $"Province {province.Id} color did not map back to its ID.");
            Assert(data.World.ProvinceGraph.GetNeighbors(province.Id).Count > 0,
                $"Province {province.Id} has no graph neighbors.");

            foreach (var neighborId in data.World.ProvinceGraph.GetNeighbors(province.Id))
            {
                Assert(neighborId != province.Id, $"Province {province.Id} is connected to itself.");
                Assert(data.World.ProvinceGraph.AreNeighbors(neighborId, province.Id),
                    $"Graph edge {province.Id} ↔ {neighborId} is not symmetric.");
            }
        }
    }

    private void VerifyEveryProvinceCanBePicked(ProvinceMap map, GameDataLoadResult data)
    {
        var transform = map.GetGlobalTransformWithCanvas();
        foreach (var province in data.World.Provinces.Values)
        {
            var mapPosition = new Vector2(province.CapitalPosition.X, province.CapitalPosition.Y);
            var screenPosition = transform * mapPosition;
            PushMouseButton(map, screenPosition, MouseButton.Left);
            Assert(_lastSelectedProvinceId == province.Id.Value,
                $"Click at {province.Name} selected province {_lastSelectedProvinceId}, expected {province.Id}.");
        }
    }

    private void PushMouseButton(ProvinceMap map, Vector2 position, MouseButton button, bool pressed = true)
    {
        map._UnhandledInput(new InputEventMouseButton
        {
            Position = position,
            ButtonIndex = button,
            Pressed = pressed
        });
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
