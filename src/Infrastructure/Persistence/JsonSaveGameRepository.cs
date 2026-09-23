using System.Text.Json;
using System.Text.Json.Serialization;

namespace AOH.Game.Infrastructure.Persistence;

public sealed class JsonSaveGameRepository
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly string _saveDirectory;

    public JsonSaveGameRepository(string saveDirectory)
    {
        _saveDirectory = Path.GetFullPath(saveDirectory);
    }

    public void Save(string slotName, GameSaveData saveData)
    {
        var path = GetSavePath(slotName);
        Directory.CreateDirectory(_saveDirectory);
        var temporaryPath = path + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(saveData, JsonOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public GameSaveData Load(string slotName)
    {
        var path = GetSavePath(slotName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Không tìm thấy bản lưu '{slotName}'.", path);
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GameSaveData>(json, JsonOptions)
                ?? throw new InvalidDataException("Tệp lưu không có dữ liệu.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Không thể đọc bản lưu '{slotName}': {exception.Message}", exception);
        }
    }

    public bool SaveExists(string slotName) => File.Exists(GetSavePath(slotName));

    public IReadOnlyList<string> ListSlots()
    {
        if (!Directory.Exists(_saveDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(_saveDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private string GetSavePath(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName) || slotName.Length > 32 ||
            slotName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')))
        {
            throw new ArgumentException("Tên ô lưu chỉ được chứa chữ, số, gạch ngang hoặc gạch dưới (tối đa 32 ký tự).", nameof(slotName));
        }

        return Path.Combine(_saveDirectory, slotName + ".json");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
