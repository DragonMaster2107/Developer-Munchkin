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
