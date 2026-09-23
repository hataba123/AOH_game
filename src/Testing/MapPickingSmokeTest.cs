using System.IO;
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
            var blockedMovement = game.MoveArmy(armyId, 8);
            Assert(!blockedMovement["success"].AsBool(), "The army should not enter another country before war is declared.");
            var declaration = game.DeclareWar(2);
            Assert(declaration["success"].AsBool(), $"War declaration failed: {declaration["message"].AsString()}");
            var movement = game.MoveArmy(armyId, 8);
            Assert(movement["success"].AsBool(), $"Army movement order failed: {movement["message"].AsString()}");
            game.SetGameSpeed(5);
            game._Process(1.4d);

            var army = game.GetArmyDetails(armyId);
            Assert(army["currentProvinceId"].AsInt32() == 8, "The army should complete movement to the enemy province.");
            var occupiedProvince = game.GetProvinceDetails(8);
            Assert(occupiedProvince["ownerCountryId"].AsInt32() == 2, "Occupation should not immediately change province ownership.");
            Assert(occupiedProvince["controllerCountryId"].AsInt32() == 1, "A victorious army should control the occupied province.");
            var enemySummary = game.GetCountrySummaries().Single(country => country["countryId"].AsInt32() == 2);
            Assert(enemySummary["armyCount"].AsInt32() > 0, "AI should recruit an army during its seven-day decision cycle.");
            var peace = game.ConcludePeace(2);
            Assert(peace["success"].AsBool(), "The player should be able to conclude peace with the enemy.");
            var settledProvince = game.GetProvinceDetails(8);
            Assert(settledProvince["controllerCountryId"].AsInt32() == 2, "Peace without enough war score should restore the occupied province.");

            var saveSlot = "smoke-" + Guid.NewGuid().ToString("N")[..12];
            var savePath = Path.Combine(OS.GetUserDataDir(), "saves", saveSlot + ".json");
            try
            {
                var summaryBeforeSave = game.GetGameSummary();
                var saveResult = game.SaveGame(saveSlot);
                Assert(saveResult["success"].AsBool(), $"Saving the game failed: {saveResult["message"].AsString()}");
                game.SetGameSpeed(0);
                var loadResult = game.LoadGame(saveSlot);
                Assert(loadResult["success"].AsBool(), $"Loading the game failed: {loadResult["message"].AsString()}");
                var summaryAfterLoad = game.GetGameSummary();
                Assert(summaryAfterLoad["date"].AsString() == summaryBeforeSave["date"].AsString(), "Loading should restore the current date.");
                Assert(summaryAfterLoad["speed"].AsInt32() == summaryBeforeSave["speed"].AsInt32(), "Loading should restore the game speed.");
                Assert(summaryAfterLoad["playerTreasury"].AsDouble() == summaryBeforeSave["playerTreasury"].AsDouble(), "Loading should restore the player treasury.");
            }
            finally
            {
                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                }
            }

            GD.Print("MapPickingSmokeTest passed: map picking, recruitment, war, army movement, AI, occupation, peace, and save/load.");
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
