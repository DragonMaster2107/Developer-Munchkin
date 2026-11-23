using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

/// <summary>Fluchtphase nach verlorenem Kampf: Würfeln, Schlimme Dinge und Aufräumen.</summary>
internal static class EscapeRules
{
    private const int EscapeThreshold = 5;

    /// <param name="rollD6">Würfelquelle; wird nur aufgerufen, wenn tatsächlich gewürfelt wird.</param>
    public static CommandResult Handle(GameState s, CardCatalog c, GameRandom rng, Func<int> rollD6, AttemptEscape cmd)
    {
        if (FleeingEntry(s, cmd.ActorId) is not { } entry || entry.UnresolvedMonsterIds.Count == 0)
        {
            return CommandResult.Rejected("Du musst gerade nicht fliehen.");
        }

        var combat = s.Combat!;
        var player = s.Players.First(p => p.Id == cmd.ActorId);
        var events = new List<GameEvent>();
        var bonus = PassiveEffects.Of(s, c, player.Id).OfType<EscapeBonus>().Sum(e => e.Amount);

        foreach (var monster in UnresolvedMonsters(combat, entry))
        {
            var def = c.Get(monster.CardId);

            if (def.NoPursuitBelowLevel is { } limit && player.Level < limit)
            {
                events.Add(new EscapeRolled(player.Id, monster.CardId, 0, 0, true));
                GameLog.Add(s, $"{player.Name} entkommt {def.Name} ohne Wurf.");
                continue;
            }

            var roll = rollD6();
            var escaped = roll + bonus >= EscapeThreshold;
            events.Add(new EscapeRolled(player.Id, monster.CardId, roll, bonus, escaped));
            if (escaped)
            {
                GameLog.Add(s, $"{player.Name} würfelt {roll} und entkommt {def.Name}.");
                continue;
            }

            GameLog.Add(s, $"{player.Name} würfelt {roll} und wird von {def.Name} erwischt.");
            events.Add(new BadStuffApplied(player.Id, monster.CardId));
            EffectApplier.ApplyToPlayer(s, c, rng, player.Id, def.BadStuff, monster.CardId, events);
        }

        Finish(s, c, combat, player.Id);
        return CommandResult.Ok(events);
    }

    /// <summary>Flucht-Einmalkarte: alle Monster des Spielers gelten als entkommen. Der Aufrufer hat bereits geprüft, dass die Karte AutoEscape hat.</summary>
    public static CommandResult PlayAutoEscape(GameState s, CardCatalog c, CombatState combat, PlayerState player, CardInstance card, List<CardInstance> source)
    {
        var monsters = FleeingEntry(s, player.Id) is { } entry ? UnresolvedMonsters(combat, entry) : [];
        if (monsters.Count == 0)
        {
            return CommandResult.Rejected("Du musst gerade nicht fliehen.");
        }

        var def = c.Get(card.CardId);
        var events = new List<GameEvent>();
        foreach (var monster in monsters)
        {
            events.Add(new EscapeRolled(player.Id, monster.CardId, 0, 0, true));
        }

        source.Remove(card);
        Decks.Discard(s, c, card);
        events.Insert(0, new CardPlayedInCombat(player.Id, card.CardId, null));
        GameLog.Add(s, $"{player.Name} spielt {def.Name} und entkommt allen Monstern.");
        Finish(s, c, combat, player.Id);
        return CommandResult.Ok(events);
    }

    private static FleeingPlayer? FleeingEntry(GameState s, string playerId) =>
        s.Phase == GamePhase.Combat && s.Combat is { Stage: CombatStage.Fleeing } combat
            ? combat.Fleeing.FirstOrDefault(f => f.PlayerId == playerId)
            : null;

    /// <summary>Die offenen Monster des Fliehenden; Ids, die nicht mehr im Kampf liegen (z. B. per Werkzeugkasten entfernt), werden übersprungen.</summary>
    private static List<CardInstance> UnresolvedMonsters(CombatState combat, FleeingPlayer entry) =>
        entry.UnresolvedMonsterIds
            .Select(id => combat.Monsters.FirstOrDefault(m => m.InstanceId == id))
            .OfType<CardInstance>()
            .ToList();

    private static void Finish(GameState s, CardCatalog c, CombatState combat, string playerId)
    {
        combat.Fleeing.RemoveAll(f => f.PlayerId == playerId);
        if (combat.Fleeing.Count == 0)
        {
            CombatCleanup.DiscardAll(s, c);
        }
    }
}
