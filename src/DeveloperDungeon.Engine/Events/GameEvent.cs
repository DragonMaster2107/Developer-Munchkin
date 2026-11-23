namespace DeveloperDungeon.Engine.Events;

public abstract record GameEvent;

public sealed record GameStarted(string StartPlayerId) : GameEvent;

public sealed record CardEquipped(string PlayerId, int InstanceId) : GameEvent;

public sealed record CardUnequipped(string PlayerId, int InstanceId) : GameEvent;

public sealed record CardDiscarded(string PlayerId, int InstanceId) : GameEvent;
