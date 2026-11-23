using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class CombatRules
{
    private const int WinLevel = 10;
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
        SetLevel(s, active, Math.Min(WinLevel, active.Level + summary.TotalRewardLevels), events);
        var gained = active.Level - from;

        if (helper is not null && helper.Level < MaxHelperLevel && PassiveEffects.Of(s, c, helper.Id).Any(e => e is HelpingGrantsLevel))
        {
            SetLevel(s, helper, helper.Level + 1, events);
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

        if (active.Level >= WinLevel)
        {
            s.WinnerId = active.Id;
            s.Phase = GamePhase.GameOver;
            events.Add(new GameWon(active.Id));
            GameLog.Add(s, $"{active.Name} erreicht Stufe {WinLevel} und gewinnt das Spiel.");
            return CommandResult.Ok(events);
        }

        CombatCleanup.DiscardAll(s, c);
        return CommandResult.Ok(events);
    }

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

    private static void SetLevel(GameState s, PlayerState player, int to, List<GameEvent> events)
    {
        if (to == player.Level)
        {
            return;
        }
        events.Add(new LevelChanged(player.Id, player.Level, to));
        GameLog.Add(s, $"{player.Name}: Stufe {player.Level} → {to}.");
        player.Level = to;
    }
}
