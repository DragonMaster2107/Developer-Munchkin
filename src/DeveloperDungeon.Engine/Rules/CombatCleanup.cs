using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class CombatCleanup
{
    /// <summary>Legt alle Kampfkarten ab, beendet den Kampf und wechselt in die Phase EndTurn.</summary>
    public static void DiscardAll(GameState s, CardCatalog c)
    {
        if (s.Combat is { } combat)
        {
            var cards = combat.Monsters
                .Concat(combat.Enhancers.Values.SelectMany(l => l))
                .Concat(combat.OneShots.Select(o => o.Card))
                .Concat(combat.OtherPlayed);
            foreach (var card in cards)
            {
                Decks.Discard(s, c, card);
            }
        }
        s.Combat = null;
        s.Phase = GamePhase.EndTurn;
    }
}
