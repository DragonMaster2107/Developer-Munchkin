using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

/// <summary>Werkzeugkasten: Eingriffe ohne Regelprüfung; jede Aktion wird mit Präfix protokolliert.</summary>
internal static class ToolboxRules
{
    private const string Prefix = "[Werkzeugkasten]";

    public static CommandResult Handle(GameState s, CardCatalog c, Func<int> rollD6, GameCommand command)
    {
        var result = command switch
        {
            ManualAdjustLevel cmd => AdjustLevel(s, cmd),
            ManualMoveCard cmd => Move(s, c, cmd),
            ManualDiscard cmd => Discard(s, c, cmd),
            ManualTakeFromDiscard cmd => TakeFromDiscard(s, c, cmd),
            ManualCombatAdjust cmd => CombatAdjust(s, cmd),
            RollDie cmd => Roll(s, rollD6, cmd),
            _ => CommandResult.Rejected("Unbekannter Befehl."),
        };
        if (result.Accepted)
        {
            EndCombatIfResolved(s, c);
            // Der Eingriff kann einem Auswählenden die Optionen genommen haben.
            ChoiceRules.DropUnresolvable(s, c);
        }
        return result;
    }

    /// <summary>Beendet den Kampf, wenn nach dem Eingriff kein Monster mehr kämpft oder niemand mehr fliehen muss.</summary>
    private static void EndCombatIfResolved(GameState s, CardCatalog c)
    {
        if (s.Phase != GamePhase.Combat || s.Combat is not { } combat)
        {
            return;
        }
        if (combat.Stage == CombatStage.Fighting && combat.Monsters.Count == 0)
        {
            CombatCleanup.DiscardAll(s, c);
            GameLog.Add(s, "Kein Monster mehr im Kampf – der Kampf endet ohne Belohnung.");
        }
        else if (combat.Stage == CombatStage.Fleeing && combat.Fleeing.Count == 0)
        {
            CombatCleanup.DiscardAll(s, c);
            GameLog.Add(s, "Niemand muss mehr fliehen – der Kampf ist vorbei.");
        }
    }

    private static CommandResult AdjustLevel(GameState s, ManualAdjustLevel cmd)
    {
        if (s.Players.FirstOrDefault(p => p.Id == cmd.TargetPlayerId) is not { } target)
        {
            return CommandResult.Rejected("Unbekannter Zielspieler.");
        }

        var events = new List<GameEvent>();
        var to = Math.Clamp(target.Level + cmd.Delta, 1, LevelRules.WinLevel);
        Done(s, cmd.ActorId, $"Stufe von {target.Name} um {cmd.Delta} geändert.", events);
        LevelRules.Set(s, target, to, events);
        LevelRules.TryWin(s, target, events);
        return CommandResult.Ok(events);
    }

    private static CommandResult Move(GameState s, CardCatalog c, ManualMoveCard cmd)
    {
        if (s.Players.FirstOrDefault(p => p.Id == cmd.ToPlayerId) is not { } target)
        {
            return CommandResult.Rejected("Unbekannter Zielspieler.");
        }
        if (Locate(s, c, cmd.InstanceId, includeDiscards: true) is not { } found)
        {
            return CommandResult.Rejected("Diese Karte gibt es nirgends im Spiel.");
        }

        found.Remove();
        ZoneList(target, cmd.ToZone).Add(found.Card);
        var events = new List<GameEvent>();
        Done(s, cmd.ActorId, $"{c.Get(found.Card.CardId).Name} wandert zu {target.Name} ({cmd.ToZone}).", events);
        return CommandResult.Ok(events);
    }

    private static CommandResult Discard(GameState s, CardCatalog c, ManualDiscard cmd)
    {
        if (Locate(s, c, cmd.InstanceId, includeDiscards: false) is not { } found)
        {
            return CommandResult.Rejected("Diese Karte liegt weder bei einem Spieler noch im Kampf.");
        }

        found.Remove();
        Decks.Discard(s, c, found.Card);
        var events = new List<GameEvent>();
        Done(s, cmd.ActorId, $"{c.Get(found.Card.CardId).Name} wird abgelegt.", events);
        return CommandResult.Ok(events);
    }

    private static CommandResult TakeFromDiscard(GameState s, CardCatalog c, ManualTakeFromDiscard cmd)
    {
        if (s.Players.FirstOrDefault(p => p.Id == cmd.ToPlayerId) is not { } target)
        {
            return CommandResult.Rejected("Unbekannter Zielspieler.");
        }
        var pile = new[] { s.TicketDiscard, s.LootDiscard }.FirstOrDefault(l => l.Any(x => x.InstanceId == cmd.InstanceId));
        if (pile is null)
        {
            return CommandResult.Rejected("Diese Karte liegt auf keiner Ablage.");
        }

        var card = pile.First(x => x.InstanceId == cmd.InstanceId);
        pile.Remove(card);
        target.Hand.Add(card);
        var events = new List<GameEvent>();
        Done(s, cmd.ActorId, $"{target.Name} nimmt {c.Get(card.CardId).Name} von der Ablage.", events);
        return CommandResult.Ok(events);
    }

    private static CommandResult CombatAdjust(GameState s, ManualCombatAdjust cmd)
    {
        if (s.Combat is not { } combat)
        {
            return CommandResult.Rejected("Es läuft gerade kein Kampf.");
        }

        if (cmd.Side == Side.Player)
        {
            combat.ManualPlayerAdjust += cmd.Delta;
        }
        else
        {
            combat.ManualMonsterAdjust += cmd.Delta;
        }
        var events = new List<GameEvent>();
        Done(s, cmd.ActorId, $"Kampfstärke {(cmd.Side == Side.Player ? "der Spieler" : "des Monsters")} um {cmd.Delta} angepasst.", events);
        return CommandResult.Ok(events);
    }

    private static CommandResult Roll(GameState s, Func<int> rollD6, RollDie cmd)
    {
        var roll = rollD6();
        var events = new List<GameEvent> { new DieRolled(cmd.ActorId, roll) };
        Done(s, cmd.ActorId, $"Würfelwurf: {roll}.", events);
        return CommandResult.Ok(events);
    }

    private static void Done(GameState s, string actorId, string description, List<GameEvent> events)
    {
        var name = s.Players.First(p => p.Id == actorId).Name;
        events.Insert(0, new ToolboxUsed(actorId, description));
        GameLog.Add(s, $"{Prefix} {name}: {description}");
    }

    private static List<CardInstance> ZoneList(PlayerState p, Zone zone) => zone switch
    {
        Zone.Hand => p.Hand,
        Zone.Equipped => p.Equipped,
        Zone.Backpack => p.Backpack,
        Zone.Department => p.Departments,
        _ => p.Languages,
    };

    private sealed record Found(CardInstance Card, Action Remove);

    private static Found? Locate(GameState s, CardCatalog c, int instanceId, bool includeDiscards)
    {
        var lists = s.Players.SelectMany(p => new[] { p.Hand, p.Equipped, p.Backpack, p.Departments, p.Languages }).ToList();
        if (s.Combat is { } combat)
        {
            lists.Add(combat.OtherPlayed);
            lists.AddRange(combat.Enhancers.Values);
        }
        if (includeDiscards)
        {
            lists.Add(s.TicketDiscard);
            lists.Add(s.LootDiscard);
        }

        foreach (var list in lists)
        {
            if (list.FirstOrDefault(x => x.InstanceId == instanceId) is { } card)
            {
                return new Found(card, () => list.Remove(card));
            }
        }

        if (s.Combat is { } fight)
        {
            if (fight.Monsters.FirstOrDefault(x => x.InstanceId == instanceId) is { } monster)
            {
                return new Found(monster, () => RemoveMonster(s, c, fight, monster));
            }
            if (fight.OneShots.FirstOrDefault(o => o.Card.InstanceId == instanceId) is { } play)
            {
                return new Found(play.Card, () => fight.OneShots.Remove(play));
            }
        }
        return null;
    }

    /// <summary>Entfernt ein Monster aus dem Kampf; seine Verstärker bleiben nicht als Waisen zurück, sondern kommen auf die Ablage.
    /// Fliehende müssen vor ihm nicht mehr fliehen; wer danach vor niemandem mehr flieht, ist fertig.</summary>
    private static void RemoveMonster(GameState s, CardCatalog c, CombatState combat, CardInstance monster)
    {
        combat.Monsters.Remove(monster);
        foreach (var entry in combat.Fleeing)
        {
            entry.UnresolvedMonsterIds.Remove(monster.InstanceId);
        }
        combat.Fleeing.RemoveAll(f => f.UnresolvedMonsterIds.Count == 0);
        if (combat.Enhancers.Remove(monster.InstanceId, out var enhancers))
        {
            foreach (var enhancer in enhancers)
            {
                Decks.Discard(s, c, enhancer);
            }
        }
    }
}
