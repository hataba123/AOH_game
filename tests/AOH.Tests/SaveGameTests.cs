using AOH.Game.Core;
using AOH.Game.Domain;
using AOH.Game.Domain.Armies;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;
using AOH.Game.Infrastructure.Persistence;
using AOH.Game.Map;
using AOH.Game.Simulation.AI;
using AOH.Game.Simulation.Military;
using Xunit;

namespace AOH.Tests;

public class SaveGameTests
{
    [Fact]
    public void JsonSaveRepository_RoundTripsWorldAndDeterministicRuntimeState()
    {
        var sourceWorld = CreateWorld();
        var playerId = new CountryId(1);
        Assert.True(sourceWorld.TryDeclareWar(playerId, new CountryId(2), new DateOnly(1444, 1, 10), out var war));
        war!.AddWarScore(playerId, 25d);
        var recruitment = new ArmyRecruitmentService().Recruit(sourceWorld, new ProvinceId(1), playerId, 1_000);
        Assert.True(recruitment.Success);
        var army = recruitment.Army!;
        army.OrderMovement(new ProvinceId(2), [new ProvinceId(2)]);

        var sourceTime = new GameTime(new DateOnly(1450, 6, 12), GameSpeed.Speed4);
        sourceTime.RestoreState(sourceTime.CurrentDate, sourceTime.Speed, 19);
        var sourceRandom = new GameRandom(77);
        sourceRandom.NextDouble();
        var sourceAi = new AiSystem(sourceTime, sourceRandom);
        for (var day = 0; day < 3; day++)
        {
            sourceAi.Process(sourceWorld);
        }

        var saveData = GameSaveData.Capture(sourceWorld, sourceTime, sourceRandom, sourceAi);
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"aoh-save-test-{Guid.NewGuid():N}");
        try
        {
            var repository = new JsonSaveGameRepository(temporaryDirectory);
            repository.Save("test-slot", saveData);
            var loadedSave = repository.Load("test-slot");

            var restoredWorld = CreateWorld();
            var restoredTime = new GameTime(new DateOnly(1444, 1, 1));
            var restoredRandom = new GameRandom(1);
            var restoredAi = new AiSystem(restoredTime, restoredRandom);
            loadedSave.Restore(restoredWorld, restoredTime, restoredRandom, restoredAi);

            Assert.Equal(sourceTime.CurrentDate, restoredTime.CurrentDate);
            Assert.Equal(sourceTime.Speed, restoredTime.Speed);
            Assert.Equal(sourceTime.TickCount, restoredTime.TickCount);
            Assert.Equal(sourceAi.DaysUntilDecision, restoredAi.DaysUntilDecision);
            Assert.Equal(sourceWorld.Countries[playerId].Treasury, restoredWorld.Countries[playerId].Treasury);
            Assert.Equal(sourceWorld.Provinces[new ProvinceId(1)].Manpower, restoredWorld.Provinces[new ProvinceId(1)].Manpower);
            Assert.Equal(war.AttackerWarScore, Assert.Single(restoredWorld.Wars.Values).AttackerWarScore);
            Assert.Equal(army.Path, Assert.Single(restoredWorld.Armies.Values).Path);
            Assert.Equal(sourceRandom.NextDouble(), restoredRandom.NextDouble());
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void JsonSaveRepository_RejectsPathTraversalSlotNames()
    {
        var repository = new JsonSaveGameRepository(Path.GetTempPath());

        Assert.Throws<ArgumentException>(() => repository.Save("../outside", new GameSaveData()));
    }

    private static GameWorld CreateWorld()
    {
        var countryOne = new Country(new CountryId(1), "An Lưu", "#C96E55", false, 1, 2_000);
        var countryTwo = new Country(new CountryId(2), "Dạ Lam", "#5D9C90", true, 2, 5_000);
        var provinces = new[]
        {
            CreateProvince(1, countryOne.Id, 0),
            CreateProvince(2, countryTwo.Id, 10)
        };
        var graph = new ProvinceGraph(new Dictionary<ProvinceId, ProvinceId[]>
        {
            [new ProvinceId(1)] = [new ProvinceId(2)],
            [new ProvinceId(2)] = [new ProvinceId(1)]
        });
        return new GameWorld([countryOne, countryTwo], provinces, graph);
    }

    private static Province CreateProvince(int id, CountryId countryId, float x)
    {
        return new Province(
            new ProvinceId(id),
            $"Tỉnh {id}",
            countryId,
            countryId,
            population: 100_000,
            economy: 1d,
            development: 1d,
            taxRate: 0.1d,
            manpower: 7_500,
            ProvinceTerrain.Plains,
            isCoastal: false,
            new MapPoint(x, 0),
            [new MapPoint(x, 0), new MapPoint(x + 1, 0), new MapPoint(x + 1, 1)]);
    }
}
