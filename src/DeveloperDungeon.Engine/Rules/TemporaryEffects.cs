using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class TemporaryEffects
{
    private static bool IsActive(GameState s, TemporaryEffect e) => s.TurnNumber > e.StartsAfterTurnNumber;

    public static int CombatModifierFor(GameState s, string playerId) =>
        s.TemporaryEffects.Where(e => e.PlayerId == playerId && IsActive(s, e)).Sum(e => e.CombatModifier);

    /// <summary>Entfernt die aktiven Effekte des Spielers; noch nicht begonnene bleiben bestehen.</summary>
    public static void ExpireAtEndOfTurn(GameState s, string playerId) =>
        s.TemporaryEffects.RemoveAll(e => e.PlayerId == playerId && IsActive(s, e));
}
