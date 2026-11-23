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
