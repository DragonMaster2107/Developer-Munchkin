namespace DeveloperDungeon.Engine.State;

/// <summary>Aktiv, wenn <c>state.TurnNumber &gt; StartsAfterTurnNumber</c>; wird am Ende des eigenen Zuges des Spielers entfernt, sofern aktiv.</summary>
public sealed class TemporaryEffect
{
    public string PlayerId { get; set; } = "";
    public int CombatModifier { get; set; }
    public int StartsAfterTurnNumber { get; set; }
    public string SourceCardId { get; set; } = "";
}
