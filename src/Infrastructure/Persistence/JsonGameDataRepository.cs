using System.Text.Json;
using System.Text.Json.Serialization;
using AOH.Game.Domain;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;
using AOH.Game.Map;
using Godot;
using GodotFileAccess = Godot.FileAccess;

namespace AOH.Game.Infrastructure.Persistence;

public sealed class JsonGameDataRepository
{
    private const string CountriesPath = "res://Data/countries.json";
    private const string ProvincesPath = "res://Data/provinces.json";
    private const string ConnectionsPath = "res://Data/province_connections.json";
    private const string ProvinceIdMapPath = "res://assets/maps/province_id_map.png";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public GameDataLoadResult Load()
    {
        var countryData = ReadJson<List<CountryData>>(CountriesPath);
        var provinceData = ReadJson<List<ProvinceData>>(ProvincesPath);
        var connectionData = ReadJson<Dictionary<int, int[]>>(ConnectionsPath);
        var countries = countryData.Select(ToCountry).ToArray();
        var provinces = provinceData.Select(ToProvince).ToArray();
        var graph = CreateProvinceGraph(connectionData);
        var colorLookup = new ProvinceColorLookup(provinces.Select(province => province.Id));
        var mapData = LoadProvinceMapData();

        var errors = WorldDefinitionValidator.Validate(countries, provinces, connectionData.ToDictionary(
            pair => new ProvinceId(pair.Key),
            pair => pair.Value.Select(neighborId => new ProvinceId(neighborId)).ToArray()), colorLookup, mapData);
        if (errors.Count > 0)
        {
            throw new InvalidDataException("Dữ liệu thế giới không hợp lệ:\n- " + string.Join("\n- ", errors));
        }

        return new GameDataLoadResult(new GameWorld(countries, provinces, graph), colorLookup, mapData);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static T ReadJson<T>(string resourcePath)
    {
        if (!GodotFileAccess.FileExists(resourcePath))
        {
            throw new FileNotFoundException($"Không tìm thấy tệp dữ liệu: {resourcePath}.");
        }

        var json = GodotFileAccess.GetFileAsString(resourcePath);
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new InvalidDataException($"Tệp JSON không có dữ liệu: {resourcePath}.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Không thể đọc JSON tại {resourcePath}: {exception.Message}", exception);
        }
    }

    private static Country ToCountry(CountryData data)
    {
        return new Country(
            new CountryId(data.CountryId),
            data.Name,
            data.MapColor,
            data.IsAiControlled,
            data.CapitalProvinceId);
    }

    private static Province ToProvince(ProvinceData data)
    {
        if (data.Vertices.Any(vertex => vertex is null || vertex.Length < 2))
        {
            throw new InvalidDataException($"Tỉnh {data.ProvinceId} có điểm hình học không hợp lệ.");
        }

        var polygon = data.Vertices.Select(vertex => new MapPoint(vertex[0], vertex[1])).ToArray();
        return new Province(
            new ProvinceId(data.ProvinceId),
            data.Name,
            new CountryId(data.OwnerCountryId),
            new CountryId(data.ControllerCountryId),
            data.Population,
            data.Economy,
            data.Development,
            data.TaxRate,
            data.Manpower,
            data.Terrain,
            data.IsCoastal,
            new MapPoint(data.CapitalPositionX, data.CapitalPositionY),
            polygon);
    }

    private static ProvinceGraph CreateProvinceGraph(Dictionary<int, int[]> connectionData)
    {
        var adjacency = connectionData.ToDictionary(
            pair => new ProvinceId(pair.Key),
            pair => pair.Value.Select(neighborId => new ProvinceId(neighborId)).ToArray());
        return new ProvinceGraph(adjacency);
    }

    private static ProvinceMapData LoadProvinceMapData()
    {
        var texture = GD.Load<Texture2D>(ProvinceIdMapPath)
            ?? throw new InvalidDataException($"Không thể tải bản đồ mã màu: {ProvinceIdMapPath}.");
        var image = texture.GetImage();
        var pixels = new byte[image.GetWidth() * image.GetHeight() * 3];

        for (var y = 0; y < image.GetHeight(); y++)
        {
            for (var x = 0; x < image.GetWidth(); x++)
            {
                var pixel = image.GetPixel(x, y);
                var pixelIndex = (y * image.GetWidth() + x) * 3;
                pixels[pixelIndex] = ToByte(pixel.R);
                pixels[pixelIndex + 1] = ToByte(pixel.G);
                pixels[pixelIndex + 2] = ToByte(pixel.B);
            }
        }

        return new ProvinceMapData(
            image.GetWidth(),
            image.GetHeight(),
            pixels,
            new Rgb24(21, 47, 55));
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
    }

    private sealed class CountryData
    {
        public int CountryId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string MapColor { get; init; } = string.Empty;

        public bool IsAiControlled { get; init; }

        public int CapitalProvinceId { get; init; }
    }

    private sealed class ProvinceData
    {
        public int ProvinceId { get; init; }

        public string Name { get; init; } = string.Empty;

        public int OwnerCountryId { get; init; }

        public int ControllerCountryId { get; init; }

        public int Population { get; init; }

        public double Economy { get; init; }

        public double Development { get; init; }

        public double TaxRate { get; init; }

        public int Manpower { get; init; }

        public ProvinceTerrain Terrain { get; init; }

        public bool IsCoastal { get; init; }

        public float CapitalPositionX { get; init; }

        public float CapitalPositionY { get; init; }

        public List<float[]> Vertices { get; init; } = [];
    }
}

public sealed class GameDataLoadResult
{
    public GameDataLoadResult(GameWorld world, ProvinceColorLookup colorLookup, ProvinceMapData mapData)
    {
        World = world;
        ColorLookup = colorLookup;
        MapData = mapData;
    }

    public GameWorld World { get; }

    public ProvinceColorLookup ColorLookup { get; }

    public ProvinceMapData MapData { get; }
}
