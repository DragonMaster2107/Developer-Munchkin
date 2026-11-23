using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

/// <summary>Verzweigt <see cref="PlayCard"/> nach Kartenart; weitere Arten kommen in späteren Tasks hinzu.</summary>
internal static class CardPlayRules
{
    public static CommandResult Handle(GameState s, CardCatalog c, GameRandom rng, PlayCard cmd)
    {
        var player = s.Players.First(p => p.Id == cmd.ActorId);
        var card = player.Hand.FirstOrDefault(x => x.InstanceId == cmd.InstanceId);
        var inBackpack = card is null && s.Phase == GamePhase.Combat
            ? player.Backpack.FirstOrDefault(x => x.InstanceId == cmd.InstanceId)
            : null;
        if (card is null && inBackpack is null)
        {
            return CommandResult.Rejected("Diese Karte hast du nicht auf der Hand.");
        }

        var played = card ?? inBackpack!;
        var source = card is not null ? player.Hand : player.Backpack;
        if (IsManualOnly(c.Get(played.CardId)))
        {
            return PlayManual(s, c, player, played, source);
        }

        if (inBackpack is not null)
        {
            return CombatCardRules.Handle(s, c, rng, player, inBackpack, player.Backpack, cmd);
        }

        return c.Get(card!.CardId).Kind switch
        {
            CardKind.Curse => PlayCurse(s, c, rng, player, card, cmd),
            CardKind.LevelUp when s.Phase != GamePhase.Combat => EconomyRules.PlayLevelUp(s, c, player, card),
            CardKind.OneShot or CardKind.MonsterEnhancer or CardKind.Special or CardKind.LevelUp or CardKind.Equipment
                => CombatCardRules.Handle(s, c, rng, player, card, player.Hand, cmd),
            _ => CommandResult.Rejected("Diese Karte kann man so nicht ausspielen."),
        };
    }

    /// <summary>Karte mit Wirkung <see cref="Manual"/> und ohne automatische Wirkung ihrer Art; Flüche laufen weiter über den Fluch-Weg.</summary>
    private static bool IsManualOnly(CardDefinition def) =>
        def.Effects.Any(e => e is Manual) && def.Kind switch
        {
            CardKind.OneShot => !def.Effects.Any(e => e is CombatBonus or AutoEscape),
            CardKind.MonsterEnhancer => !def.Effects.Any(e => e is MonsterModifier),
            CardKind.LevelUp => !def.Effects.Any(e => e is GainLevels),
            CardKind.Special => !def.Effects.Any(e => e is MaxDepartments or MaxLanguages or AddMonsterToCombat),
            _ => false,
        };

    /// <summary>Manuelle Karte: ablegen, Text groß zeigen, protokollieren; umgesetzt wird sie mit dem Werkzeugkasten.
    /// Einmalkarten und Verstärker jederzeit (außer im Aufbau), Spezial- und Stufe-hoch-Karten im eigenen Zug oder in jedem Kampf.</summary>
    private static CommandResult PlayManual(GameState s, CardCatalog c, PlayerState player, CardInstance card, List<CardInstance> source)
    {
        var def = c.Get(card.CardId);
        if (def.Kind is CardKind.OneShot or CardKind.MonsterEnhancer)
        {
            if (s.Phase == GamePhase.Setup)
            {
                return CommandResult.Rejected("Im Aufbau kann man diese Karte nicht ausspielen.");
            }
        }
        else if (s.Phase != GamePhase.Combat && !CommandGuard.IsOwnTurnOutsideCombat(s, player.Id))
        {
            return CommandResult.Rejected("Diese Karte spielst du in deinem Zug oder während eines Kampfs.");
        }

        source.Remove(card);
        Decks.Discard(s, c, card);
        var events = new List<GameEvent>();
        if (s.Phase == GamePhase.Combat)
        {
            events.Add(new CardPlayedInCombat(player.Id, card.CardId, null));
        }
        events.Add(new ManualEffectShown(card.CardId));
        GameLog.Add(s, $"{player.Name} spielt {def.Name} (manuelle Wirkung).");
        return CommandResult.Ok(events);
    }

    private static CommandResult PlayCurse(GameState s, CardCatalog c, GameRandom rng, PlayerState player, CardInstance card, PlayCard cmd)
    {
        if (s.Phase == GamePhase.Setup)
        {
            return CommandResult.Rejected("Im Aufbau kann man keine Flüche ausspielen.");
        }
        if (cmd.TargetPlayerId is null || s.Players.All(p => p.Id != cmd.TargetPlayerId))
        {
            return CommandResult.Rejected("Ein Fluch braucht einen Zielspieler.");
        }

        player.Hand.Remove(card);
        var events = new List<GameEvent>();
        Curses.Apply(s, c, rng, player.Id, cmd.TargetPlayerId, card, events);
        return CommandResult.Ok(events);
    }
}
