using AOH.Game.Domain;
using AOH.Game.Domain.Armies;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;

namespace AOH.Game.Simulation.Military;

public sealed class ArmyRecruitmentService
{
    public const double CostPerSoldier = 0.5d;

    public ArmyRecruitmentResult Recruit(GameWorld world, ProvinceId provinceId, CountryId countryId, int soldiers)
    {
        if (soldiers <= 0)
        {
            return ArmyRecruitmentResult.Failed("Số binh sĩ phải lớn hơn 0.");
        }

        if (!world.TryGetProvince(provinceId, out var province) || !world.TryGetCountry(countryId, out var country))
        {
            return ArmyRecruitmentResult.Failed("Không tìm thấy tỉnh hoặc quốc gia.");
        }

        if (province.OwnerCountryId != countryId || province.ControllerCountryId != countryId)
        {
            return ArmyRecruitmentResult.Failed("Chỉ có thể tuyển quân tại tỉnh do quốc gia kiểm soát.");
        }

        if (province.Manpower < soldiers)
        {
            return ArmyRecruitmentResult.Failed("Tỉnh không đủ nhân lực dự bị.");
        }

        var cost = soldiers * CostPerSoldier;
        if (!country.TryRecruitSoldiers(soldiers, cost))
        {
            return ArmyRecruitmentResult.Failed("Ngân khố hoặc tổng nhân lực quốc gia không đủ.");
        }

        if (!province.TryRecruitSoldiers(soldiers))
        {
            return ArmyRecruitmentResult.Failed("Tỉnh không đủ nhân lực dự bị.");
        }

        var nextArmyId = world.Armies.Count == 0 ? 1 : world.Armies.Keys.Max(armyId => armyId.Value) + 1;
        var army = new Army(new ArmyId(nextArmyId), countryId, provinceId, soldiers);
        if (!world.TryAddArmy(army))
        {
            return ArmyRecruitmentResult.Failed("Không thể thêm đội quân vào thế giới.");
        }

        return ArmyRecruitmentResult.Recruited(army);
    }
}

public sealed record ArmyRecruitmentResult(bool Success, Army? Army, string Message)
{
    public static ArmyRecruitmentResult Recruited(Army army) => new(true, army, $"Đã tuyển {army.Soldiers:N0} binh sĩ.");

    public static ArmyRecruitmentResult Failed(string message) => new(false, null, message);
}
