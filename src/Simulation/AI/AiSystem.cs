using AOH.Game.Core;
using AOH.Game.Domain;
using AOH.Game.Domain.Armies;
using AOH.Game.Domain.Countries;
using AOH.Game.Domain.Provinces;
using AOH.Game.Map;
using AOH.Game.Simulation.Military;

namespace AOH.Game.Simulation.AI;

public sealed class AiSystem : IGameSystem
{
    private const int DaysBetweenDecisions = 7;
    private const int RecruitmentSize = 1_000;
    private const double MinimumWarTreasury = 2_000d;
    private readonly GameTime _gameTime;
    private readonly GameRandom _random;
    private readonly ArmyRecruitmentService _recruitmentService = new();
    private ProvincePathfinder? _pathfinder;
    private int _daysUntilDecision;
    private IReadOnlyList<AiDecision> _lastDecisions = Array.Empty<AiDecision>();

    public AiSystem(GameTime gameTime, GameRandom random)
    {
        _gameTime = gameTime;
        _random = random;
    }

    public IReadOnlyList<AiDecision> LastDecisions => _lastDecisions;

    public void Process(GameWorld world)
    {
        _daysUntilDecision++;
        if (_daysUntilDecision < DaysBetweenDecisions)
        {
            return;
        }

        _daysUntilDecision = 0;
        _pathfinder ??= new ProvincePathfinder(world);
        var decisions = new List<AiDecision>();
        foreach (var country in world.Countries.Values.Where(country => country.IsAiControlled).OrderBy(country => country.Id.Value))
        {
            var candidates = Evaluate(world, country);
            var decision = candidates
                .OrderByDescending(candidate => candidate.Utility)
                .ThenBy(candidate => candidate.Action)
                .ThenBy(candidate => candidate.TargetId)
                .First();
            decisions.Add(decision);
            Execute(world, decision);
        }

        _lastDecisions = decisions;
    }

    private List<AiDecision> Evaluate(GameWorld world, Country country)
    {
        var candidates = new List<AiDecision>();
        var armies = world.Armies.Values.Where(army => army.OwnerCountryId == country.Id).ToArray();
        var totalSoldiers = armies.Sum(army => army.Soldiers);
        var militaryScore = CalculateMilitaryScore(country, totalSoldiers);
        var economyScore = CalculateEconomyScore(country);
        var atWar = world.Wars.Values.Any(war => war.IsActive && war.IsParticipant(country.Id));
        var expansionScore = atWar ? 15d : Math.Clamp(country.Treasury / 5_000d * 100d, 20d, 100d);

        AddRecruitmentCandidate(world, country, militaryScore, economyScore, atWar, candidates);
        AddWarCandidate(world, country, totalSoldiers, militaryScore, expansionScore, candidates);
        AddMilitaryMovementCandidates(world, country, armies, candidates);

        if (candidates.Count == 0)
        {
            candidates.Add(CreateDecision(country.Id, AiActionType.SaveMoney, 5d, 0, 0));
        }

        return candidates;
    }

    private void AddRecruitmentCandidate(
        GameWorld world,
        Country country,
        double militaryScore,
        double economyScore,
        bool atWar,
        ICollection<AiDecision> candidates)
    {
        if (country.Treasury < RecruitmentSize * ArmyRecruitmentService.CostPerSoldier || country.Manpower < RecruitmentSize)
        {
            return;
        }

        var recruitmentProvince = world.Provinces.Values
            .Where(province => province.OwnerCountryId == country.Id &&
                               province.ControllerCountryId == country.Id &&
                               province.Manpower >= RecruitmentSize)
            .OrderByDescending(province => province.Manpower)
            .ThenBy(province => province.Id.Value)
            .FirstOrDefault();
        if (recruitmentProvince is null)
        {
            return;
        }

        var utility = 50d + ((100d - militaryScore) * 0.2d) + (economyScore * 0.1d) - (atWar ? 8d : 0d);
        candidates.Add(CreateDecision(country.Id, AiActionType.RecruitArmy, utility, recruitmentProvince.Id.Value, 0));
    }

    private void AddWarCandidate(
        GameWorld world,
        Country country,
        int totalSoldiers,
        double militaryScore,
        double expansionScore,
        ICollection<AiDecision> candidates)
    {
        if (totalSoldiers < RecruitmentSize * 2 || country.Treasury < MinimumWarTreasury ||
            world.Wars.Values.Any(war => war.IsActive && war.IsParticipant(country.Id)))
        {
            return;
        }

        var neighboringCountries = new HashSet<CountryId>();
        foreach (var province in world.Provinces.Values.Where(province => province.OwnerCountryId == country.Id))
        {
            foreach (var neighborId in world.ProvinceGraph.GetNeighbors(province.Id))
            {
                if (world.TryGetProvince(neighborId, out var neighbor) && neighbor.OwnerCountryId != country.Id)
                {
                    neighboringCountries.Add(neighbor.OwnerCountryId);
                }
            }
        }

        foreach (var targetCountryId in neighboringCountries.OrderBy(id => id.Value))
        {
            if (!world.TryGetCountry(targetCountryId, out _ ) || world.IsAtWar(country.Id, targetCountryId))
            {
                continue;
            }

            var utility = 68d + (expansionScore * 0.15d) + (militaryScore * 0.1d);
            candidates.Add(CreateDecision(country.Id, AiActionType.DeclareWar, utility, targetCountryId.Value, 0));
        }
    }

    private void AddMilitaryMovementCandidates(
        GameWorld world,
        Country country,
        IReadOnlyList<Army> armies,
        ICollection<AiDecision> candidates)
    {
        var idleArmies = armies.Where(army => army.Path.Count == 0).OrderBy(army => army.Id.Value).ToArray();
        if (idleArmies.Length == 0)
        {
            return;
        }

        var occupiedProvinces = world.Provinces.Values
            .Where(province => province.OwnerCountryId == country.Id && province.ControllerCountryId != country.Id &&
                               world.IsAtWar(country.Id, province.ControllerCountryId))
            .OrderBy(province => province.Id.Value)
            .ToArray();
        var defenseMove = FindBestMove(world, country.Id, idleArmies, occupiedProvinces);
        if (defenseMove is { } defense)
        {
            candidates.Add(CreateDecision(country.Id, AiActionType.DefendProvince, 96d, defense.ArmyId.Value, defense.TargetProvinceId.Value));
        }

        var enemyProvinces = world.Provinces.Values
            .Where(province => province.OwnerCountryId != country.Id && world.IsAtWar(country.Id, province.OwnerCountryId))
            .OrderBy(province => province.Id.Value)
            .ToArray();
        var attackMove = FindBestMove(world, country.Id, idleArmies, enemyProvinces);
        if (attackMove is { } attack)
        {
            candidates.Add(CreateDecision(country.Id, AiActionType.AttackProvince, 82d, attack.ArmyId.Value, attack.TargetProvinceId.Value));
        }
    }

    private (ArmyId ArmyId, ProvinceId TargetProvinceId)? FindBestMove(
        GameWorld world,
        CountryId countryId,
        IReadOnlyList<Army> armies,
        IReadOnlyList<Province> targets)
    {
        if (_pathfinder is null)
        {
            return null;
        }

        var bestLength = int.MaxValue;
        (ArmyId ArmyId, ProvinceId TargetProvinceId)? bestMove = null;
        foreach (var army in armies)
        {
            foreach (var target in targets)
            {
                var path = _pathfinder.FindPath(army.CurrentProvinceId, target.Id, countryId);
                if (path.Count <= 1 || path.Count >= bestLength)
                {
                    continue;
                }

                bestLength = path.Count;
                bestMove = (army.Id, target.Id);
            }
        }

        return bestMove;
    }

    private void Execute(GameWorld world, AiDecision decision)
    {
        switch (decision.Action)
        {
            case AiActionType.RecruitArmy:
                _recruitmentService.Recruit(world, new ProvinceId(decision.TargetId), decision.CountryId, RecruitmentSize);
                break;
            case AiActionType.DeclareWar:
                world.TryDeclareWar(decision.CountryId, new CountryId(decision.TargetId), _gameTime.CurrentDate, out _);
                break;
            case AiActionType.DefendProvince:
            case AiActionType.AttackProvince:
                if (world.Armies.TryGetValue(new ArmyId(decision.TargetId), out var army) && _pathfinder is not null)
                {
                    var targetId = new ProvinceId(decision.SecondaryTargetId);
                    var path = _pathfinder.FindPath(army.CurrentProvinceId, targetId, decision.CountryId);
                    if (path.Count > 1)
                    {
                        army.OrderMovement(targetId, path.Skip(1).ToArray());
                    }
                }

                break;
            case AiActionType.SaveMoney:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(decision), decision.Action, "Quyết định AI không được hỗ trợ.");
        }
    }

    private AiDecision CreateDecision(CountryId countryId, AiActionType action, double utility, int targetId, int secondaryTargetId)
    {
        return new AiDecision(countryId, action, Math.Clamp(utility + _random.NextDouble(-1.5d, 1.5d), 0d, 100d), targetId, secondaryTargetId);
    }

    private static double CalculateMilitaryScore(Country country, int soldiers)
    {
        var expectedForce = Math.Max(1d, country.Population * 0.01d);
        return Math.Clamp(soldiers / expectedForce * 100d, 0d, 100d);
    }

    private static double CalculateEconomyScore(Country country)
    {
        var expectedTreasury = Math.Max(1d, country.Income * 30d);
        return Math.Clamp(country.Treasury / expectedTreasury * 100d, 0d, 100d);
    }
}
