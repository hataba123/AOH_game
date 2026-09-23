using AOH.Game.Domain.Countries;

namespace AOH.Game.Domain.Diplomacy;

public sealed class War
{
    private readonly HashSet<CountryId> _attackerIds;
    private readonly HashSet<CountryId> _defenderIds;

    public War(WarId id, CountryId attackerCountryId, CountryId defenderCountryId, DateOnly startDate)
    {
        if (attackerCountryId == defenderCountryId)
        {
            throw new ArgumentException("Một quốc gia không thể tuyên chiến với chính mình.", nameof(defenderCountryId));
        }

        Id = id;
        _attackerIds = [attackerCountryId];
        _defenderIds = [defenderCountryId];
        StartDate = startDate;
    }

    public WarId Id { get; }

    public IReadOnlySet<CountryId> AttackerIds => _attackerIds;

    public IReadOnlySet<CountryId> DefenderIds => _defenderIds;

    public DateOnly StartDate { get; }

    public double AttackerWarScore { get; private set; }

    public bool IsActive { get; private set; } = true;

    public bool IsParticipant(CountryId countryId) => _attackerIds.Contains(countryId) || _defenderIds.Contains(countryId);

    public bool AreEnemies(CountryId first, CountryId second) =>
        (_attackerIds.Contains(first) && _defenderIds.Contains(second)) ||
        (_defenderIds.Contains(first) && _attackerIds.Contains(second));

    public bool IsAttacker(CountryId countryId) => _attackerIds.Contains(countryId);

    public void AddWarScore(CountryId scoringCountryId, double amount)
    {
        if (!IsActive || !IsParticipant(scoringCountryId) || amount < 0d)
        {
            return;
        }

        AttackerWarScore = Math.Clamp(
            AttackerWarScore + (IsAttacker(scoringCountryId) ? amount : -amount),
            -100d,
            100d);
    }

    public void Conclude() => IsActive = false;
}
