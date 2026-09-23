using System.Diagnostics;
using AOH.Game.Core;
using AOH.Game.Domain;
using AOH.Game.Domain.Armies;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;
using AOH.Game.Map;
using AOH.Game.Simulation;
using AOH.Game.Simulation.AI;
using AOH.Game.Simulation.Economy;
using AOH.Game.Simulation.Military;
using AOH.Game.Simulation.Population;
using Godot;

namespace AOH.Game.Tests;

[GlobalClass]
public partial class WorldScaleBenchmark : Node
{
    private const int GridWidth = 100;
    private const int GridHeight = 50;
    private const int CountryWidth = 5;
    private const int CountryHeight = 5;
    private const int SimulationDays = 8;
    private const int ExpectedCountryCount = (GridWidth / CountryWidth) * (GridHeight / CountryHeight);

    public override void _Ready()
    {
        try
        {
            var worldTimer = Stopwatch.StartNew();
            var world = CreateWorld();
            worldTimer.Stop();

            var gameTime = new GameTime(new DateOnly(1444, 1, 1));
            var random = new GameRandom(20260923);
            var aiSystem = new AiSystem(gameTime, random);
            var simulation = new SimulationEngine(
                world,
                gameTime,
                new EconomySystem(),
                new PopulationSystem(),
                new ArmyMovementSystem(),
                new CombatSystem(random),
                aiSystem);

            var simulationTimer = Stopwatch.StartNew();
            for (var day = 0; day < SimulationDays; day++)
            {
                simulation.AdvanceTick();
            }

            simulationTimer.Stop();

            var routeTimer = Stopwatch.StartNew();
            var pathfinder = new ProvincePathfinder(world);
            var route = pathfinder.FindPath(new ProvinceId(1), new ProvinceId(405), new CountryId(1));
            routeTimer.Stop();

            if (world.Provinces.Count != GridWidth * GridHeight ||
                world.Armies.Count < GridWidth * GridHeight ||
                world.Countries.Count != ExpectedCountryCount ||
                route.Count <= 1 ||
                gameTime.TickCount != SimulationDays)
            {
                throw new InvalidOperationException("Benchmark không tạo đủ thực thể hoặc không hoàn tất được đường đi/mô phỏng.");
            }

            GD.Print("WorldScaleBenchmark passed.");
            GD.Print($"Khởi tạo thế giới: {worldTimer.Elapsed.TotalMilliseconds:F2} ms");
            GD.Print($"Mô phỏng {SimulationDays} ngày: {simulationTimer.Elapsed.TotalMilliseconds:F2} ms; trung bình {simulation.AverageTickDurationMilliseconds:F2} ms/ngày; lần cuối {simulation.LastTickDurationMilliseconds:F2} ms");
            GD.Print($"Thực thể: {world.Provinces.Count:N0} tỉnh, {world.Countries.Count:N0} quốc gia, {world.Armies.Count:N0} quân, {world.Wars.Count:N0} cuộc chiến; quyết định AI gần nhất: {aiSystem.LastDecisions.Count:N0}");
            GD.Print($"Tìm đường A*: {routeTimer.Elapsed.TotalMilliseconds:F2} ms; {route.Count:N0} tỉnh trên tuyến.");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"WorldScaleBenchmark failed: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private static GameWorld CreateWorld()
    {
        var countries = new List<Country>(ExpectedCountryCount);
        for (var countryNumber = 1; countryNumber <= ExpectedCountryCount; countryNumber++)
        {
            countries.Add(new Country(
                new CountryId(countryNumber),
                $"Quốc gia {countryNumber:000}",
                CreateMapColor(countryNumber),
                isAiControlled: countryNumber != 1,
                capitalProvinceId: GetProvinceId((countryNumber - 1) / (GridWidth / CountryWidth) * CountryHeight, ((countryNumber - 1) % (GridWidth / CountryWidth)) * CountryWidth)));
        }

        var provinces = new List<Province>(GridWidth * GridHeight);
        var adjacency = new Dictionary<ProvinceId, ProvinceId[]>(GridWidth * GridHeight);
        for (var row = 0; row < GridHeight; row++)
        {
            for (var column = 0; column < GridWidth; column++)
            {
                var provinceId = new ProvinceId(GetProvinceId(row, column));
                var countryRow = row / CountryHeight;
                var countryColumn = column / CountryWidth;
                var ownerCountryId = new CountryId((countryRow * (GridWidth / CountryWidth)) + countryColumn + 1);
                var left = column * 20f;
                var top = row * 20f;
                var polygon = new MapPoint[]
                {
                    new(left, top),
                    new(left + 20f, top),
                    new(left + 20f, top + 20f),
                    new(left, top + 20f)
                };

                provinces.Add(new Province(
                    provinceId,
                    $"Tỉnh {provinceId.Value:0000}",
                    ownerCountryId,
                    ownerCountryId,
                    population: 25_000,
                    economy: 1d,
                    development: 1d,
                    taxRate: 0.1d,
                    manpower: 1_500,
                    ProvinceTerrain.Plains,
                    isCoastal: false,
                    new MapPoint(left + 10f, top + 10f),
                    polygon));

                adjacency.Add(provinceId, GetNeighbors(row, column));
            }
        }

        var world = new GameWorld(countries, provinces, new ProvinceGraph(adjacency));
        foreach (var province in provinces)
        {
            if (!world.TryAddArmy(new Army(new ArmyId(province.Id.Value), province.OwnerCountryId, province.Id, 100)))
            {
                throw new InvalidOperationException($"Không thể tạo quân tại tỉnh {province.Id}.");
            }
        }

        return world;
    }

    private static ProvinceId[] GetNeighbors(int row, int column)
    {
        var neighbors = new List<ProvinceId>(4);
        if (row > 0)
        {
            neighbors.Add(new ProvinceId(GetProvinceId(row - 1, column)));
        }

        if (column > 0)
        {
            neighbors.Add(new ProvinceId(GetProvinceId(row, column - 1)));
        }

        if (column + 1 < GridWidth)
        {
            neighbors.Add(new ProvinceId(GetProvinceId(row, column + 1)));
        }

        if (row + 1 < GridHeight)
        {
            neighbors.Add(new ProvinceId(GetProvinceId(row + 1, column)));
        }

        return neighbors.ToArray();
    }

    private static int GetProvinceId(int row, int column) => (row * GridWidth) + column + 1;

    private static string CreateMapColor(int countryNumber)
    {
        var red = 55 + ((countryNumber * 37) % 150);
        var green = 55 + ((countryNumber * 67) % 150);
        var blue = 55 + ((countryNumber * 97) % 150);
        return $"#{red:X2}{green:X2}{blue:X2}";
    }
}
