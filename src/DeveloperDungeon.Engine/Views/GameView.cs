using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Views;

public sealed record PublicPlayerView(
    string Id,
    string Name,
    string Color,
    int Level,
    string Title,
    int HandCount,
    IReadOnlyList<CardInstance> Equipped,
    IReadOnlyList<CardInstance> Backpack,
    IReadOnlyList<CardInstance> Departments,
    IReadOnlyList<CardInstance> Languages,
    int BasePower);

public sealed record CombatView(CombatState State, CombatSummary Summary);

public sealed record GameView(
    GamePhase Phase,
    string ActivePlayerId,
    int TurnNumber,
    IReadOnlyList<PublicPlayerView> Players,
    IReadOnlyList<CardInstance>? MyHand,
    CombatView? Combat,
    int TicketDeckCount,
    int LootDeckCount,
    CardInstance? TicketDiscardTop,
    CardInstance? LootDiscardTop,
    PendingChoice? CurrentChoice,
    string? WinnerId,
    IReadOnlyList<LogEntry> RecentLog);
