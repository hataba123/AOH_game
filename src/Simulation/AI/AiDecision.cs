using AOH.Game.Domain.Countries;

namespace AOH.Game.Simulation.AI;

public readonly record struct AiDecision(
    CountryId CountryId,
    AiActionType Action,
    double Utility,
    int TargetId,
    int SecondaryTargetId);
