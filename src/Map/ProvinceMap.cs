using AOH.Game.Domain;
using AOH.Game.Domain.Armies;
using AOH.Game.Domain.Provinces;
using AOH.Game.Infrastructure.Persistence;
using Godot;

namespace AOH.Game.Map;

public partial class ProvinceMap : Node2D
{
    private const float MapWidth = 1600f;
    private const float MapHeight = 1000f;
    private const float MinimumZoom = 0.45f;
    private const float MaximumZoom = 1.55f;
    private const float InitialZoom = 0.7f;
    private static readonly Color SeaFill = new("#152F37");
    private static readonly Color GridColor = new(0.64f, 0.78f, 0.76f, 0.08f);
    private static readonly Color ProvinceBorder = new("#D4D1BC");
    private static readonly Color CoastlineColor = new("#E9DDC3");
    private static readonly Color SelectionColor = new("#FFD38A");
    private static readonly Color HoverColor = new("#8FE0D0");

    private readonly Camera2D _camera;
    private GameWorld? _world;
    private ProvinceColorLookup? _colorLookup;
    private ProvinceMapData? _mapData;
    private ProvinceId? _selectedProvinceId;
    private ProvinceId? _hoveredProvinceId;
    private ArmyId? _selectedArmyId;
    private bool _isPanning;

    public event Action<ProvinceId?>? ProvinceSelectionChanged;

    public event Action<ProvinceId?>? ProvinceHoverChanged;

    public event Action<ArmyId?>? ArmySelectionChanged;

    public ProvinceMap()
    {
        _camera = new Camera2D
        {
            Name = "MapCamera",
            Position = new Vector2(515f, 480f),
            Zoom = Vector2.One * InitialZoom,
            IgnoreRotation = true
        };
        AddChild(_camera);
    }

    [Signal]
    public delegate void ProvinceSelectedEventHandler(int provinceId);

    [Signal]
    public delegate void ProvinceHoveredEventHandler(int provinceId);

    [Signal]
    public delegate void ArmySelectedEventHandler(int armyId);

    public void Configure(GameWorld world, ProvinceColorLookup colorLookup, ProvinceMapData mapData)
    {
        _world = world;
        _colorLookup = colorLookup;
        _mapData = mapData;
        QueueRedraw();
    }

    public void SelectArmy(ArmyId armyId)
    {
        _selectedArmyId = armyId;
        QueueRedraw();
    }

    public override void _Ready()
    {
        _camera.MakeCurrent();
    }

    public override void _Draw()
    {
        if (_world is null)
        {
            return;
        }

        DrawRect(new Rect2(0f, 0f, MapWidth, MapHeight), SeaFill);
        DrawSeaGrid();

        foreach (var province in _world.Provinces.Values)
        {
            var country = _world.Countries[province.OwnerCountryId];
            var color = Color.FromHtml(country.MapColor);
            var polygon = ToVector2Array(province.Polygon);
            DrawColoredPolygon(polygon, color);
        }

        DrawProvinceBorders();
        DrawCoastline();
        DrawProvinceLabels();
        DrawProvinceHighlight(_selectedProvinceId, SelectionColor, 4f, 0.18f);

        if (_hoveredProvinceId != _selectedProvinceId)
        {
            DrawProvinceHighlight(_hoveredProvinceId, HoverColor, 2.5f, 0.14f);
        }

        DrawArmies();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_world is null || _mapData is null || _colorLookup is null)
        {
            return;
        }

        if (inputEvent is InputEventMouseButton mouseButton)
        {
            HandleMouseButton(mouseButton);
            return;
        }

        if (inputEvent is InputEventMouseMotion mouseMotion)
        {
            if (_isPanning)
            {
                var zoom = Math.Max(_camera.Zoom.X, 0.001f);
                _camera.Position -= mouseMotion.Relative / zoom;
                GetViewport().SetInputAsHandled();
                return;
            }

            UpdateHoveredProvince(mouseMotion.Position);
        }
    }

    public override void _Process(double delta)
    {
        if (_world is null)
        {
            return;
        }

        var direction = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
        {
            direction.Y -= 1f;
        }

        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
        {
            direction.Y += 1f;
        }

        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
        {
            direction.X -= 1f;
        }

        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
        {
            direction.X += 1f;
        }

        if (direction.LengthSquared() > 0f)
        {
            _camera.Position += direction.Normalized() * (520f * (float)delta / _camera.Zoom.X);
        }
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.ButtonIndex is MouseButton.Right or MouseButton.Middle)
        {
            _isPanning = mouseButton.Pressed;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!mouseButton.Pressed || mouseButton.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.Left))
        {
            return;
        }

        if (mouseButton.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            ZoomAtMouse(mouseButton.ButtonIndex == MouseButton.WheelUp ? 1.12f : 1f / 1.12f, mouseButton.Position);
            GetViewport().SetInputAsHandled();
            return;
        }

        var mapPosition = GetMapPosition(mouseButton.Position);
        if (TrySelectArmyAt(mapPosition))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_mapData is not null && _colorLookup is not null &&
            _mapData.TryGetProvinceId(Mathf.FloorToInt(mapPosition.X), Mathf.FloorToInt(mapPosition.Y), _colorLookup, out var provinceId))
        {
            _selectedProvinceId = provinceId;
            EmitSignal(SignalName.ProvinceSelected, provinceId.Value);
            ProvinceSelectionChanged?.Invoke(provinceId);
        }
        else
        {
            _selectedProvinceId = null;
            EmitSignal(SignalName.ProvinceSelected, -1);
            ProvinceSelectionChanged?.Invoke(null);
        }

        QueueRedraw();
        GetViewport().SetInputAsHandled();
    }

    private void ZoomAtMouse(float scale, Vector2 viewportPosition)
    {
        var currentZoom = _camera.Zoom.X;
        var nextZoom = Math.Clamp(currentZoom * scale, MinimumZoom, MaximumZoom);
        if (Mathf.IsEqualApprox(currentZoom, nextZoom))
        {
            return;
        }

        var mapPositionBeforeZoom = GetMapPosition(viewportPosition);
        _camera.Zoom = Vector2.One * nextZoom;
        var mapPositionAfterZoom = GetMapPosition(viewportPosition);
        _camera.Position += mapPositionBeforeZoom - mapPositionAfterZoom;
    }

    private void UpdateHoveredProvince(Vector2 viewportPosition)
    {
        if (_mapData is null || _colorLookup is null)
        {
            return;
        }

        var mapPosition = GetMapPosition(viewportPosition);
        ProvinceId? provinceId = _mapData.TryGetProvinceId(Mathf.FloorToInt(mapPosition.X), Mathf.FloorToInt(mapPosition.Y), _colorLookup, out var hoveredId)
            ? hoveredId
            : null;

        if (provinceId == _hoveredProvinceId)
        {
            return;
        }

        _hoveredProvinceId = provinceId;
        EmitSignal(SignalName.ProvinceHovered, provinceId?.Value ?? -1);
        ProvinceHoverChanged?.Invoke(provinceId);
        QueueRedraw();
    }

    private Vector2 GetMapPosition(Vector2 viewportPosition)
    {
        return GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
    }

    private void DrawSeaGrid()
    {
        for (var x = 0; x <= MapWidth; x += 160)
        {
            DrawLine(new Vector2(x, 0), new Vector2(x, MapHeight), GridColor, 1f);
        }

        for (var y = 0; y <= MapHeight; y += 160)
        {
            DrawLine(new Vector2(0, y), new Vector2(MapWidth, y), GridColor, 1f);
        }

        for (var index = 0; index < 13; index++)
        {
            var x = 80f + (index * 131f);
            var y = 100f + ((index % 3) * 300f);
            DrawArc(new Vector2(x, y), 58f, 0.35f, 2.4f, 32, new Color(0.64f, 0.78f, 0.76f, 0.08f), 1f);
            DrawArc(new Vector2(x, y), 78f, 0.35f, 2.4f, 32, new Color(0.64f, 0.78f, 0.76f, 0.05f), 1f);
        }
    }

    private void DrawProvinceBorders()
    {
        if (_world is null)
        {
            return;
        }

        foreach (var province in _world.Provinces.Values)
        {
            DrawPolyline(ClosedPolygon(province.Polygon), ProvinceBorder, 2f, true);
        }
    }

    private void DrawCoastline()
    {
        if (_world is null)
        {
            return;
        }

        var edgeCounts = new Dictionary<MapEdgeKey, (MapPoint Start, MapPoint End, int Count)>();
        foreach (var province in _world.Provinces.Values)
        {
            for (var index = 0; index < province.Polygon.Count; index++)
            {
                var start = province.Polygon[index];
                var end = province.Polygon[(index + 1) % province.Polygon.Count];
                var key = MapEdgeKey.Create(start, end);
                if (edgeCounts.TryGetValue(key, out var edge))
                {
                    edgeCounts[key] = (edge.Start, edge.End, edge.Count + 1);
                }
                else
                {
                    edgeCounts.Add(key, (start, end, 1));
                }
            }
        }

        foreach (var edge in edgeCounts.Values)
        {
            if (edge.Count == 1)
            {
                DrawLine(ToVector(edge.Start), ToVector(edge.End), new Color(0.08f, 0.13f, 0.14f, 0.55f), 7f, true);
                DrawLine(ToVector(edge.Start), ToVector(edge.End), CoastlineColor, 3f, true);
            }
        }
    }

    private void DrawProvinceLabels()
    {
        if (_world is null)
        {
            return;
        }

        var font = ThemeDB.FallbackFont;
        foreach (var province in _world.Provinces.Values)
        {
            var position = new Vector2(province.CapitalPosition.X, province.CapitalPosition.Y);
            DrawCircle(position, 8f, new Color(0.08f, 0.13f, 0.14f, 0.8f));
            DrawCircle(position, 5f, new Color("#F3E7CA"));
            DrawString(font, position + new Vector2(0f, 27f), province.Name, HorizontalAlignment.Center, -1f, 17, new Color(0.08f, 0.13f, 0.14f, 0.8f));
            DrawString(font, position + new Vector2(0f, 25f), province.Name, HorizontalAlignment.Center, -1f, 17, new Color("#F2E9D4"));
        }
    }

    private void DrawArmies()
    {
        if (_world is null)
        {
            return;
        }

        var stackCounts = new Dictionary<ProvinceId, int>();
        foreach (var army in _world.Armies.Values.OrderBy(army => army.Id.Value))
        {
            if (!_world.TryGetProvince(army.CurrentProvinceId, out var province))
            {
                continue;
            }

            stackCounts.TryGetValue(province.Id, out var stackIndex);
            stackCounts[province.Id] = stackIndex + 1;
            var offset = new Vector2(((stackIndex % 3) - 1) * 18f, (stackIndex / 3) * 18f);
            var position = new Vector2(province.CapitalPosition.X, province.CapitalPosition.Y) + offset;
            var countryColor = Color.FromHtml(_world.Countries[army.OwnerCountryId].MapColor);

            if (_selectedArmyId == army.Id)
            {
                DrawCircle(position, 15f, new Color("#ffd166"));
            }

            DrawCircle(position, 12f, new Color("#101820"));
            DrawCircle(position, 9f, countryColor);
            DrawArc(position, 9f, 0f, Mathf.Tau, 24, new Color("#f8fafc"), 1.5f, true);
            DrawString(ThemeDB.FallbackFont, position + new Vector2(15f, 5f), army.Soldiers.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), HorizontalAlignment.Left, -1f, 14, new Color("#f8fafc"));
        }
    }

    private bool TrySelectArmyAt(Vector2 mapPosition)
    {
        if (_world is null)
        {
            return false;
        }

        ArmyId? nearestArmyId = null;
        var nearestDistanceSquared = 20f * 20f;
        var stackCounts = new Dictionary<ProvinceId, int>();
        foreach (var army in _world.Armies.Values.OrderBy(army => army.Id.Value))
        {
            if (!_world.TryGetProvince(army.CurrentProvinceId, out var province))
            {
                continue;
            }

            stackCounts.TryGetValue(province.Id, out var stackIndex);
            stackCounts[province.Id] = stackIndex + 1;
            var offset = new Vector2(((stackIndex % 3) - 1) * 18f, (stackIndex / 3) * 18f);
            var markerPosition = new Vector2(province.CapitalPosition.X, province.CapitalPosition.Y) + offset;
            var distanceSquared = markerPosition.DistanceSquaredTo(mapPosition);
            if (distanceSquared <= nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestArmyId = army.Id;
            }
        }

        if (nearestArmyId is not { } selectedArmyId)
        {
            return false;
        }

        _selectedArmyId = selectedArmyId;
        EmitSignal(SignalName.ArmySelected, selectedArmyId.Value);
        ArmySelectionChanged?.Invoke(selectedArmyId);
        QueueRedraw();
        return true;
    }

    private void DrawProvinceHighlight(ProvinceId? provinceId, Color color, float width, float alpha)
    {
        if (_world is null || provinceId is null || !_world.TryGetProvince(provinceId.Value, out var province))
        {
            return;
        }

        var fill = color;
        fill.A = alpha;
        DrawColoredPolygon(ToVector2Array(province.Polygon), fill);
        DrawPolyline(ClosedPolygon(province.Polygon), color, width, true);
    }

    private static Vector2[] ToVector2Array(IReadOnlyList<MapPoint> polygon)
    {
        return polygon.Select(ToVector).ToArray();
    }

    private static Vector2[] ClosedPolygon(IReadOnlyList<MapPoint> polygon)
    {
        var points = new Vector2[polygon.Count + 1];
        for (var index = 0; index < polygon.Count; index++)
        {
            points[index] = ToVector(polygon[index]);
        }

        points[^1] = points[0];
        return points;
    }

    private static Vector2 ToVector(MapPoint point) => new(point.X, point.Y);

    private readonly record struct MapEdgeKey(int FirstX, int FirstY, int SecondX, int SecondY)
    {
        public static MapEdgeKey Create(MapPoint first, MapPoint second)
        {
            var firstX = (int)MathF.Round(first.X * 10f);
            var firstY = (int)MathF.Round(first.Y * 10f);
            var secondX = (int)MathF.Round(second.X * 10f);
            var secondY = (int)MathF.Round(second.Y * 10f);

            return firstX < secondX || (firstX == secondX && firstY <= secondY)
                ? new MapEdgeKey(firstX, firstY, secondX, secondY)
                : new MapEdgeKey(secondX, secondY, firstX, firstY);
        }
    }
}
