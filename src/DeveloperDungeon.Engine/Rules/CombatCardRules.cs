using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

/// <summary>Karten, die im laufenden Kampf (Stage Fighting) ausgespielt werden; alle Spieler dürfen eingreifen.</summary>
internal static class CombatCardRules
{
    public static CommandResult Handle(GameState s, CardCatalog c, GameRandom rng, PlayerState player, CardInstance card, List<CardInstance> source, PlayCard cmd)
    {
        if (s.Phase != GamePhase.Combat || s.Combat is not { Stage: CombatStage.Fighting } combat)
        {
            return CommandResult.Rejected("Diese Karte kann man nur im laufenden Kampf ausspielen.");
        }

        var def = c.Get(card.CardId);
        return def.Kind switch
        {
            CardKind.OneShot => PlayOneShot(s, c, rng, combat, player, card, def, source, cmd),
            CardKind.MonsterEnhancer => PlayEnhancer(s, combat, player, card, source, cmd),
            CardKind.Special when def.Effects.OfType<AddMonsterToCombat>().Any() => PlayWanderingBug(s, c, combat, player, card, source, cmd),
            CardKind.LevelUp or CardKind.Equipment => CommandResult.Rejected("Das kannst du im Kampf nicht ausspielen."),
            _ => CommandResult.Rejected("Diese Karte kann man so nicht ausspielen."),
        };
    }

    private static CommandResult PlayOneShot(GameState s, CardCatalog c, GameRandom rng, CombatState combat, PlayerState player, CardInstance card, CardDefinition def, List<CardInstance> source, PlayCard cmd)
    {
        var events = new List<GameEvent>();
        if (def.Effects.OfType<AutoEscape>().Any())
        {
            return CommandResult.Rejected("Das hilft erst bei der Flucht.");
        }

        if (def.Effects.OfType<CombatBonus>().Any())
        {
            if (cmd.Side is not { } side)
            {
                return CommandResult.Rejected("Wähle, auf welcher Seite die Karte wirken soll.");
            }

            source.Remove(card);
            combat.OneShots.Add(new OneShotPlay { Card = card, Side = side, PlayedBy = player.Id });
            events.Add(new CardPlayedInCombat(player.Id, card.CardId, side));
            GameLog.Add(s, $"{player.Name} spielt {def.Name} für {(side == Side.Player ? "die Spieler" : "das Monster")}.");
            // Der Kampfbonus wird vom StrengthCalculator gezählt; hier nur die Folgewirkungen.
            EffectApplier.ApplyToPlayer(s, c, rng, player.Id, def.Effects.Where(e => e is NextTurnPenalty or Manual), card.CardId, events);
            return CommandResult.Ok(events);
        }

        if (def.Effects.OfType<Manual>().Any())
        {
            source.Remove(card);
            Decks.Discard(s, c, card);
            events.Add(new CardPlayedInCombat(player.Id, card.CardId, null));
            events.Add(new ManualEffectShown(card.CardId));
            GameLog.Add(s, $"{player.Name} spielt {def.Name} (manuelle Wirkung).");
            return CommandResult.Ok(events);
        }

        return CommandResult.Rejected("Diese Karte kann man so nicht ausspielen.");
    }

    private static CommandResult PlayEnhancer(GameState s, CombatState combat, PlayerState player, CardInstance card, List<CardInstance> source, PlayCard cmd)
    {
        if (cmd.TargetMonsterInstanceId is not { } target || combat.Monsters.All(m => m.InstanceId != target))
        {
            return CommandResult.Rejected("Wähle ein Monster im Kampf als Ziel.");
        }

        source.Remove(card);
        if (!combat.Enhancers.TryGetValue(target, out var list))
        {
            combat.Enhancers[target] = list = [];
        }
        list.Add(card);
        GameLog.Add(s, $"{player.Name} verstärkt ein Monster.");
        return CommandResult.Ok([new CardPlayedInCombat(player.Id, card.CardId, null)]);
    }

    private static CommandResult PlayWanderingBug(GameState s, CardCatalog c, CombatState combat, PlayerState player, CardInstance card, List<CardInstance> source, PlayCard cmd)
    {
        var monster = cmd.SecondaryInstanceId is { } id
            ? player.Hand.FirstOrDefault(x => x.InstanceId == id && c.Get(x.CardId).Kind == CardKind.Monster)
            : null;
        if (monster is null)
        {
            return CommandResult.Rejected("Wähle ein Monster aus deiner Hand.");
        }

        source.Remove(card);
        player.Hand.Remove(monster);
        combat.Monsters.Add(monster);
        combat.OtherPlayed.Add(card);
        GameLog.Add(s, $"{player.Name} spielt {c.Get(card.CardId).Name}: {c.Get(monster.CardId).Name} greift ebenfalls an.");
        return CommandResult.Ok([new CardPlayedInCombat(player.Id, card.CardId, null)]);
    }
}
