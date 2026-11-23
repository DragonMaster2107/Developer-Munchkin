using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class CombatStart
{
    /// <summary>Eröffnet einen Kampf gegen das Monster; die Karte darf in keiner Zone mehr liegen.</summary>
    public static void Begin(GameState s, CardInstance monster)
    {
        s.Combat = new CombatState { Stage = CombatStage.Fighting, Monsters = [monster] };
        s.Phase = GamePhase.Combat;
    }
}
