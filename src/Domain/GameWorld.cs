using System.Collections.ObjectModel;
using AOH.Game.Domain.Armies;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Diplomacy;
using AOH.Game.Domain.Provinces;

namespace AOH.Game.Domain;

public sealed class GameWorld
{
    private readonly Dictionary<ArmyId, Army> _armies = [];
    private readonly Dictionary<WarId, War> _wars = [];

    public GameWorld(
        IEnumerable<Country> countries,
        IEnumerable<Province> provinces,
        ProvinceGraph provinceGraph)
    {
        Countries = new ReadOnlyDictionary<CountryId, Country>(countries.ToDictionary(country => country.Id));
        Provinces = new ReadOnlyDictionary<ProvinceId, Province>(provinces.ToDictionary(province => province.Id));
        ProvinceGraph = provinceGraph;
        RecalculateDemographics();
    }

    public IReadOnlyDictionary<CountryId, Country> Countries { get; }

    public IReadOnlyDictionary<ProvinceId, Province> Provinces { get; }

    public ProvinceGraph ProvinceGraph { get; }

    public IReadOnlyDictionary<ArmyId, Army> Armies => _armies;

    public IReadOnlyDictionary<WarId, War> Wars => _wars;

    public bool TryAddArmy(Army army)
    {
        if (!Countries.ContainsKey(army.OwnerCountryId) || !Provinces.ContainsKey(army.CurrentProvinceId))
        {
            return false;
        }

        return _armies.TryAdd(army.Id, army);
    }

    public bool TryDeclareWar(CountryId attackerCountryId, CountryId defenderCountryId, DateOnly startDate, out War? war)
    {
        war = null;
        if (attackerCountryId == defenderCountryId ||
            !Countries.ContainsKey(attackerCountryId) ||
            !Countries.ContainsKey(defenderCountryId) ||
            IsAtWar(attackerCountryId, defenderCountryId))
        {
            return false;
        }

        var nextWarId = _wars.Count == 0 ? 1 : _wars.Keys.Max(warId => warId.Value) + 1;
        war = new War(new WarId(nextWarId), attackerCountryId, defenderCountryId, startDate);
        _wars.Add(war.Id, war);
        return true;
    }

    public void ClearRuntimeEntities()
    {
        _armies.Clear();
        _wars.Clear();
    }

    public bool TryAddWar(War war)
    {
        if (!war.IsActive || !war.AttackerIds.All(Countries.ContainsKey) || !war.DefenderIds.All(Countries.ContainsKey) ||
            war.AttackerIds.Any(war.DefenderIds.Contains) ||
            war.AttackerIds.Any(attacker => war.DefenderIds.Any(defender => IsAtWar(attacker, defender))))
        {
            return false;
        }

        return _wars.TryAdd(war.Id, war);
    }

    public bool IsAtWar(CountryId first, CountryId second) =>
        _wars.Values.Any(war => war.IsActive && war.AreEnemies(first, second));

    public bool TryConcludePeace(WarId warId, CountryId requestingCountryId)
    {
        if (!_wars.TryGetValue(warId, out var war) || !war.IsActive || !war.IsParticipant(requestingCountryId))
        {
            return false;
        }

        var victor = war.AttackerWarScore > 0d
            ? war.AttackerIds.First()
            : war.AttackerWarScore < 0d
                ? war.DefenderIds.First()
                : (CountryId?)null;
        var availableConcessions = (int)(Math.Abs(war.AttackerWarScore) / 20d);

        foreach (var province in Provinces.Values.OrderBy(province => province.Id.Value))
        {
            if (!war.IsParticipant(province.OwnerCountryId) || !war.IsParticipant(province.ControllerCountryId))
            {
                continue;
            }

            if (victor is { } victorId &&
                province.ControllerCountryId == victorId &&
                province.OwnerCountryId != victorId &&
                availableConcessions > 0)
            {
                province.TransferOwnership(victorId);
                availableConcessions--;
            }
            else
            {
                province.SetController(province.OwnerCountryId);
            }
        }

        war.Conclude();
        RecalculateDemographics();
        return true;
    }

    public bool TryGetActiveWar(CountryId first, CountryId second, out War war)
    {
        war = _wars.Values.FirstOrDefault(candidate => candidate.IsActive && candidate.AreEnemies(first, second))!;
        return war is not null;
    }

    public bool TryRemoveArmy(ArmyId armyId) => _armies.Remove(armyId);

    public void RecalculateDemographics()
    {
        foreach (var country in Countries.Values)
        {
            country.SetDemographics(0, 0);
        }

        foreach (var province in Provinces.Values)
        {
            var owner = Countries[province.OwnerCountryId];
            owner.SetDemographics(owner.Population + province.Population, owner.Manpower + province.Manpower);
        }
    }

    public bool TryGetProvince(ProvinceId provinceId, out Province province)
    {
        return Provinces.TryGetValue(provinceId, out province!);
    }

    public bool TryGetCountry(CountryId countryId, out Country country)
    {
        return Countries.TryGetValue(countryId, out country!);
    }
}
