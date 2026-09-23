using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;
using AOH.Game.Map;
using System.Globalization;

namespace AOH.Game.Domain;

public static class WorldDefinitionValidator
{
    public static IReadOnlyList<string> Validate(
        IReadOnlyList<Country> countries,
        IReadOnlyList<Province> provinces,
        IReadOnlyDictionary<ProvinceId, ProvinceId[]> adjacency,
        ProvinceColorLookup colorLookup,
        ProvinceMapData mapData)
    {
        var errors = new List<string>();
        var countryIds = new HashSet<CountryId>();
        var provinceIds = new HashSet<ProvinceId>();
        var countryColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var country in countries)
        {
            if (!countryIds.Add(country.Id))
            {
                errors.Add($"Country ID trùng: {country.Id}.");
            }

            if (!countryColors.Add(country.MapColor))
            {
                errors.Add($"Màu lãnh thổ bị trùng: {country.MapColor}.");
            }

            if (string.IsNullOrWhiteSpace(country.Name))
            {
                errors.Add($"Quốc gia {country.Id} chưa có tên.");
            }

            if (country.Treasury < 0)
            {
                errors.Add($"Ngân khố của {country.Name} không thể âm khi bắt đầu game.");
            }

            if (country.MapColor.Length != 7 || country.MapColor[0] != '#' ||
                !int.TryParse(country.MapColor.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            {
                errors.Add($"Màu bản đồ của {country.Name} phải có dạng #RRGGBB.");
            }
        }

        foreach (var province in provinces)
        {
            if (!provinceIds.Add(province.Id))
            {
                errors.Add($"Province ID trùng: {province.Id}.");
            }

            if (!countryIds.Contains(province.OwnerCountryId))
            {
                errors.Add($"Tỉnh {province.Id} tham chiếu quốc gia sở hữu không tồn tại: {province.OwnerCountryId}.");
            }

            if (!countryIds.Contains(province.ControllerCountryId))
            {
                errors.Add($"Tỉnh {province.Id} tham chiếu quốc gia kiểm soát không tồn tại: {province.ControllerCountryId}.");
            }

            if (string.IsNullOrWhiteSpace(province.Name))
            {
                errors.Add($"Tỉnh {province.Id} chưa có tên.");
            }

            if (province.Population < 0 || province.Economy < 0 || province.Development <= 0 ||
                province.TaxRate is < 0 or > 1 || province.Manpower < 0)
            {
                errors.Add($"Tỉnh {province.Id} có chỉ số dân số, kinh tế, thuế hoặc nhân lực ngoài phạm vi hợp lệ.");
            }

            if (province.Polygon.Count < 3)
            {
                errors.Add($"Tỉnh {province.Id} cần ít nhất 3 điểm để vẽ.");
            }
            else if (province.Polygon.Any(point => point.X < 0 || point.X >= mapData.Width || point.Y < 0 || point.Y >= mapData.Height))
            {
                errors.Add($"Tọa độ hình học của tỉnh {province.Id} nằm ngoài bản đồ.");
            }

            if (!colorLookup.TryGetColor(province.Id, out _))
            {
                errors.Add($"Tỉnh {province.Id} chưa có màu trong ProvinceColorLookup.");
            }
        }

        foreach (var country in countries)
        {
            var capitalId = new ProvinceId(country.CapitalProvinceId);
            if (!provinces.Any(province => province.Id == capitalId))
            {
                errors.Add($"Thủ phủ {country.CapitalProvinceId} của {country.Name} không tồn tại.");
            }
            else if (provinces.First(province => province.Id == capitalId).OwnerCountryId != country.Id)
            {
                errors.Add($"Tỉnh thủ phủ {country.CapitalProvinceId} không thuộc {country.Name}.");
            }
        }

        foreach (var provinceId in provinceIds)
        {
            if (!adjacency.ContainsKey(provinceId))
            {
                errors.Add($"Chưa khai báo kết nối cho tỉnh {provinceId}.");
            }
        }

        foreach (var connection in adjacency)
        {
            if (!provinceIds.Contains(connection.Key))
            {
                errors.Add($"Province graph có ID không tồn tại: {connection.Key}.");
                continue;
            }

            var uniqueNeighbors = new HashSet<ProvinceId>();
            foreach (var neighbor in connection.Value)
            {
                if (!provinceIds.Contains(neighbor))
                {
                    errors.Add($"Tỉnh {connection.Key} kết nối tới tỉnh không tồn tại {neighbor}.");
                }
                else if (neighbor == connection.Key)
                {
                    errors.Add($"Tỉnh {connection.Key} không thể kết nối với chính nó.");
                }
                else if (!uniqueNeighbors.Add(neighbor))
                {
                    errors.Add($"Kết nối {connection.Key} → {neighbor} bị lặp.");
                }
                else if (!adjacency.TryGetValue(neighbor, out var reverseConnections) || !reverseConnections.Contains(connection.Key))
                {
                    errors.Add($"Kết nối giữa {connection.Key} và {neighbor} chưa đối xứng.");
                }
            }
        }

        if (colorLookup.Count != provinces.Count)
        {
            errors.Add($"Bảng màu có {colorLookup.Count} ID, nhưng dữ liệu có {provinces.Count} tỉnh.");
        }

        var pixelCounts = provinceIds.ToDictionary(provinceId => provinceId, _ => 0);
        for (var y = 0; y < mapData.Height; y++)
        {
            for (var x = 0; x < mapData.Width; x++)
            {
                if (!mapData.TryGetProvinceId(x, y, colorLookup, out var provinceId))
                {
                    var pixelIndex = (y * mapData.Width + x) * 3;
                    var pixel = new Rgb24(mapData.RgbPixels[pixelIndex], mapData.RgbPixels[pixelIndex + 1], mapData.RgbPixels[pixelIndex + 2]);
                    if (pixel != mapData.SeaColor)
                    {
                        errors.Add($"Bản đồ ID có màu không xác định tại pixel ({x}, {y}).");
                        return errors;
                    }

                    continue;
                }

                if (!provinceIds.Contains(provinceId))
                {
                    errors.Add($"Province ID {provinceId} xuất hiện trên bản đồ nhưng không có trong JSON.");
                    return errors;
                }

                pixelCounts[provinceId]++;
            }
        }

        foreach (var province in provinces)
        {
            if (pixelCounts.TryGetValue(province.Id, out var pixelCount) && pixelCount == 0)
            {
                errors.Add($"Tỉnh {province.Id} không có pixel tương ứng trên bản đồ mã màu.");
            }
        }

        return errors;
    }
}
