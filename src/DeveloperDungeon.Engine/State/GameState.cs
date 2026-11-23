namespace DeveloperDungeon.Engine.State;

public sealed class GameState
{
    /// <summary>Aktuelles Format des Spielstands; <c>Game.Load</c> lehnt andere Versionen ab.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public GamePhase Phase { get; set; }
    public List<PlayerState> Players { get; set; } = [];
    public int ActivePlayerIndex { get; set; }
    public int TurnNumber { get; set; }
    public List<CardInstance> TicketDeck { get; set; } = [];
    public List<CardInstance> TicketDiscard { get; set; } = [];
    public List<CardInstance> LootDeck { get; set; } = [];
    public List<CardInstance> LootDiscard { get; set; } = [];
    public CombatState? Combat { get; set; }
    public List<PendingChoice> PendingChoices { get; set; } = [];
    public List<TemporaryEffect> TemporaryEffects { get; set; } = [];
    public ulong RngState { get; set; }
    public string? WinnerId { get; set; }
    public List<LogEntry> Log { get; set; } = [];
}
