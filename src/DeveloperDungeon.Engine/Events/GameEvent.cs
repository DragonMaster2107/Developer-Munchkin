using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Events;

public abstract record GameEvent;

public sealed record GameStarted(string StartPlayerId) : GameEvent;

public sealed record CardEquipped(string PlayerId, int InstanceId) : GameEvent;

public sealed record CardUnequipped(string PlayerId, int InstanceId) : GameEvent;

public sealed record CardDiscarded(string PlayerId, int InstanceId) : GameEvent;

public sealed record CursePlayed(string ById, string TargetId, string CardId) : GameEvent;

public sealed record CurseBlocked(string TargetId, string CardId) : GameEvent;

public sealed record LevelChanged(string PlayerId, int From, int To) : GameEvent;

public sealed record CardsLost(string PlayerId, IReadOnlyList<int> InstanceIds) : GameEvent;

public sealed record ChoiceRequested(string PlayerId, ChoiceKind Kind) : GameEvent;

public sealed record ChoiceResolved(string PlayerId, int InstanceId) : GameEvent;

public sealed record ManualEffectShown(string CardId) : GameEvent;

public sealed record TicketOpened(string PlayerId, string CardId) : GameEvent;

public sealed record CombatStarted(string PlayerId, string MonsterCardId) : GameEvent;

/// <summary>Karte verdeckt gezogen; die Karte selbst wird nicht verraten.</summary>
public sealed record CardDrawn(string PlayerId, Cards.Deck Deck) : GameEvent;

public sealed record CardsGiven(string FromId, string ToId, int Count) : GameEvent;

public sealed record TurnEnded(string PlayerId, string NextPlayerId) : GameEvent;

public sealed record CombatWon(string PlayerId, int LevelsGained, int LootDrawn) : GameEvent;

public sealed record CombatLost(string PlayerId) : GameEvent;

public sealed record GameWon(string PlayerId) : GameEvent;
