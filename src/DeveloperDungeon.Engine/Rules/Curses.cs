using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class Curses
{
    /// <summary>Wendet einen Fluch auf das Ziel an (sofern nicht immun) und legt die Karte ab. Die Karte darf in keiner Zone mehr liegen.</summary>
    public static void Apply(GameState s, CardCatalog c, GameRandom rng, string byId, string targetId, CardInstance curse, List<GameEvent> events)
    {
        var def = c.Get(curse.CardId);
        var target = s.Players.First(p => p.Id == targetId);
        events.Add(new CursePlayed(byId, targetId, def.Id));
        // Ein aus dem Ticket gezogener Fluch trifft den Ziehenden selbst; dann gibt es keinen Verfluchenden zu nennen.
        GameLog.Add(s, byId == targetId
            ? $"{def.Name} trifft {target.Name}."
            : $"{s.Players.First(p => p.Id == byId).Name} verflucht {target.Name} mit {def.Name}.");

        var immune = PassiveEffects.Of(s, c, targetId).OfType<CurseImmunity>().Any(e => def.Tags.Contains(e.CurseTag));
        if (immune)
        {
            events.Add(new CurseBlocked(targetId, def.Id));
            GameLog.Add(s, $"{target.Name} ist gegen {def.Name} immun.");
        }
        else
        {
            EffectApplier.ApplyToPlayer(s, c, rng, targetId, def.Effects, def.Id, events);
        }
        Decks.Discard(s, c, curse);
    }
}
