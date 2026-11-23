using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class CombatRules
{
    private const int MaxHelperLevel = 9;

    public static CommandResult Handle(GameState s, CardCatalog c, GameRandom rng, ResolveCombat cmd)
    {
        if (s.Players[s.ActivePlayerIndex].Id != cmd.ActorId)
        {
            return CommandResult.Rejected("Du bist nicht am Zug.");
        }
        if (s.Phase != GamePhase.Combat || s.Combat is not { Stage: CombatStage.Fighting } combat)
        {
            return CommandResult.Rejected("Es läuft gerade kein Kampf, der ausgewertet werden kann.");
        }

        var active = s.Players[s.ActivePlayerIndex];
        var summary = StrengthCalculator.Summarize(s, c);
        var events = new List<GameEvent>();

        if (!summary.PlayerWouldWin)
        {
            combat.Stage = CombatStage.Fleeing;
            var monsterIds = combat.Monsters.Select(m => m.InstanceId).ToList();
            var fleeing = new List<string> { active.Id };
            if (combat.HelperId is not null)
            {
                fleeing.Add(combat.HelperId);
            }
            combat.Fleeing = fleeing.Select(id => new FleeingPlayer { PlayerId = id, UnresolvedMonsterIds = monsterIds.ToList() }).ToList();
            events.Add(new CombatLost(active.Id));
            GameLog.Add(s, $"{active.Name} verliert den Kampf ({summary.PlayerStrength} gegen {summary.MonsterStrength}) und muss fliehen.");
            return CommandResult.Ok(events);
        }

        var helper = combat.HelperId is null ? null : s.Players.First(p => p.Id == combat.HelperId);
        var from = active.Level;
        LevelRules.Set(s, active, Math.Min(LevelRules.WinLevel, active.Level + summary.TotalRewardLevels), events);
        var gained = active.Level - from;

        if (helper is not null && helper.Level < MaxHelperLevel && PassiveEffects.Of(s, c, helper.Id).Any(e => e is HelpingGrantsLevel))
        {
            LevelRules.Set(s, helper, helper.Level + 1, events);
        }

        var drawn = DrawLoot(s, rng, summary.TotalRewardLoot);
        var helperShare = helper is null ? 0 : Math.Min(combat.HelperLootShare, drawn.Count);
        for (var i = 0; i < drawn.Count; i++)
        {
            var receiver = i < helperShare ? helper! : active;
            receiver.Hand.Add(drawn[i]);
            events.Add(new CardDrawn(receiver.Id, Deck.Loot));
        }
        events.Add(new CombatWon(active.Id, gained, drawn.Count));
        GameLog.Add(s, $"{active.Name} gewinnt den Kampf ({summary.PlayerStrength} gegen {summary.MonsterStrength}): +{gained} Stufen, {drawn.Count} Loot-Karten.");

        if (LevelRules.TryWin(s, active, events))
        {
            return CommandResult.Ok(events);
        }

        CombatCleanup.DiscardAll(s, c);
        return CommandResult.Ok(events);
    }

    public static CommandResult Handle(GameState s, OfferHelp cmd)
    {
        if (Fighting(s) is not { } combat)
        {
            return CommandResult.Rejected("Es läuft gerade kein Kampf.");
        }
        if (IsActive(s, cmd.ActorId))
        {
            return CommandResult.Rejected("Du kämpfst selbst und kannst dir nicht selbst helfen.");
        }
        if (combat.HelperId is not null)
        {
            return CommandResult.Rejected("Es gibt bereits einen Helfer.");
        }
        if (cmd.LootShare < 0)
        {
            return CommandResult.Rejected("Der Loot-Anteil darf nicht negativ sein.");
        }

        combat.Offers.RemoveAll(o => o.PlayerId == cmd.ActorId);
        combat.Offers.Add(new HelpOffer { PlayerId = cmd.ActorId, LootShare = cmd.LootShare });
        GameLog.Add(s, $"{NameOf(s, cmd.ActorId)} bietet Hilfe an (Loot-Anteil: {cmd.LootShare}).");
        return CommandResult.Ok([new HelpOffered(cmd.ActorId, cmd.LootShare)]);
    }

    public static CommandResult Handle(GameState s, AcceptHelp cmd)
    {
        if (Fighting(s) is not { } combat)
        {
            return CommandResult.Rejected("Es läuft gerade kein Kampf.");
        }
        if (!IsActive(s, cmd.ActorId))
        {
            return CommandResult.Rejected("Nur der aktive Spieler kann Hilfe annehmen.");
        }
        if (combat.HelperId is not null)
        {
            return CommandResult.Rejected("Es gibt bereits einen Helfer.");
        }
        var offer = combat.Offers.FirstOrDefault(o => o.PlayerId == cmd.HelperId);
        if (offer is null)
        {
            return CommandResult.Rejected("Dieser Spieler hat keine Hilfe angeboten.");
        }

        combat.HelperId = offer.PlayerId;
        combat.HelperLootShare = offer.LootShare;
        combat.Offers.Clear();
        GameLog.Add(s, $"{NameOf(s, offer.PlayerId)} hilft im Kampf (Loot-Anteil: {offer.LootShare}).");
        return CommandResult.Ok([new HelpAccepted(offer.PlayerId)]);
    }

    private static CombatState? Fighting(GameState s) =>
        s.Phase == GamePhase.Combat && s.Combat is { Stage: CombatStage.Fighting } combat ? combat : null;

    private static bool IsActive(GameState s, string playerId) => s.Players[s.ActivePlayerIndex].Id == playerId;

    private static string NameOf(GameState s, string playerId) => s.Players.First(p => p.Id == playerId).Name;

    private static List<CardInstance> DrawLoot(GameState s, GameRandom rng, int count)
    {
        var drawn = new List<CardInstance>();
        for (var i = 0; i < count; i++)
        {
            if (Decks.Draw(s, rng, Deck.Loot) is { } card)
            {
                drawn.Add(card);
            }
        }
        return drawn;
    }
}
