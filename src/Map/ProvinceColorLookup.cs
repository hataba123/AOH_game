using AOH.Game.Domain.Provinces;

namespace AOH.Game.Map;

public sealed class ProvinceColorLookup
{
    private readonly Dictionary<uint, ProvinceId> _provinceByColor;
    private readonly Dictionary<ProvinceId, Rgb24> _colorByProvince;

    public ProvinceColorLookup(IEnumerable<ProvinceId> provinceIds)
    {
        _provinceByColor = new Dictionary<uint, ProvinceId>();
        _colorByProvince = new Dictionary<ProvinceId, Rgb24>();

        foreach (var provinceId in provinceIds)
        {
            var color = Rgb24.FromProvinceId(provinceId.Value);
            if (!_colorByProvince.TryAdd(provinceId, color))
            {
                throw new InvalidDataException($"Province ID bị trùng trong bảng màu: {provinceId}.");
            }

            if (!_provinceByColor.TryAdd(color.PackedValue, provinceId))
            {
                throw new InvalidDataException($"Màu mã hóa bản đồ bị trùng tại tỉnh {provinceId}.");
            }
        }
    }

    public int Count => _provinceByColor.Count;

    public bool TryGetProvinceId(Rgb24 color, out ProvinceId provinceId)
    {
        return _provinceByColor.TryGetValue(color.PackedValue, out provinceId);
    }

    public bool TryGetColor(ProvinceId provinceId, out Rgb24 color)
    {
        return _colorByProvince.TryGetValue(provinceId, out color);
    }
}
