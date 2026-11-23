using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class CombatInteractionTests
{
    private static Game Fight(string monster, Func<GameBuilder, GameBuilder>? setup = null)
    {
        var builder = GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop(monster);
        var game = (setup?.Invoke(builder) ?? builder).Build();
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
        return game;
    }

    private static int IdOf(Game g, int player, string cardId) =>
        g.State.Players[player - 1].Hand.First(c => c.CardId == cardId).InstanceId;

    [Fact]
    public void OneShotOnPlayerSide_TurnsLossIntoWin()
    {
        var game = Fight("monster-l3-legacy", b => b.Hand(1, "oneshot-plus5"));
        Assert.False(StrengthCalculator.Summarize(game.State, game.Catalog).PlayerWouldWin);

        var result = game.Execute(new PlayCard("p1", IdOf(game, 1, "oneshot-plus5"), Side: Side.Player));

        Assert.True(result.Accepted);
        Assert.True(StrengthCalculator.Summarize(game.State, game.Catalog).PlayerWouldWin);
        Assert.Empty(game.State.Players[0].Hand);
        Assert.Contains(result.Events, e => e is CardPlayedInCombat { PlayerId: "p1", CardId: "oneshot-plus5", Side: Side.Player });
    }

    [Fact]
    public void OneShotFromBackpack_IsAllowed()
    {
        var game = Fight("monster-l3-legacy", b => b.Backpack(1, "oneshot-plus5"));
        var id = game.State.Players[0].Backpack[0].InstanceId;

        Assert.True(game.Execute(new PlayCard("p1", id, Side: Side.Player)).Accepted);
        Assert.Empty(game.State.Players[0].Backpack);
    }

    [Fact]
    public void OneShotOnMonsterSide_ByOpponent_TurnsWinIntoLoss()
    {
        var game = Fight("monster-l3-legacy", b => b.Level(1, 4).Hand(2, "oneshot-plus5"));
        Assert.True(StrengthCalculator.Summarize(game.State, game.Catalog).PlayerWouldWin);

        var result = game.Execute(new PlayCard("p2", IdOf(game, 2, "oneshot-plus5"), Side: Side.Monster));

        Assert.True(result.Accepted);
        Assert.False(StrengthCalculator.Summarize(game.State, game.Catalog).PlayerWouldWin);
    }

    [Fact]
    public void OneShotWithoutSide_IsRejected()
    {
        var game = Fight("monster-l3-legacy", b => b.Hand(1, "oneshot-plus5"));

        var result = game.Execute(new PlayCard("p1", IdOf(game, 1, "oneshot-plus5")));

        Assert.False(result.Accepted);
        Assert.Single(game.State.Players[0].Hand);
        Assert.Empty(game.State.Combat!.OneShots);
    }

    [Fact]
    public void ColdCoffeePenalty_HitsPlayerWhoPlayedIt_NextTurn()
    {
        var game = Fight("monster-l1", b => b.Hand(2, "oneshot-plus2"));

        game.Execute(new PlayCard("p2", IdOf(game, 2, "oneshot-plus2"), Side: Side.Monster));

        var penalty = Assert.Single(game.State.TemporaryEffects);
        Assert.Equal("p2", penalty.PlayerId);
        Assert.Equal(-1, penalty.CombatModifier);
        Assert.Equal(0, TemporaryEffects.CombatModifierFor(game.State, "p2"));
        // Bonus nur einmal gezählt: Monster 1 + 2.
        Assert.Equal(3, StrengthCalculator.Summarize(game.State, game.Catalog).MonsterStrength);
    }

    [Fact]
    public void OneShot_OutsideCombat_IsRejected()
    {
        var game = GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).Hand(1, "oneshot-plus5").Build();

        var result = game.Execute(new PlayCard("p1", IdOf(game, 1, "oneshot-plus5"), Side: Side.Player));

        Assert.False(result.Accepted);
        Assert.Single(game.State.Players[0].Hand);
    }

    [Fact]
    public void AutoEscapeOneShot_DuringFighting_IsRejected()
    {
        var game = Fight("monster-l1", b => b.Hand(1, "oneshot-remote"));

        var result = game.Execute(new PlayCard("p1", IdOf(game, 1, "oneshot-remote"), Side: Side.Player));

        Assert.False(result.Accepted);
        Assert.Single(game.State.Players[0].Hand);
    }

    [Fact]
    public void RemoteDay_DuringFighting_IsRejected()
    {
        var game = Fight("monster-l1", b => b.Hand(1, "oneshot-remote"));

        Assert.False(game.Execute(new PlayCard("p1", IdOf(game, 1, "oneshot-remote"))).Accepted);
    }

    [Fact]
    public void ManualOneShot_ShowsEffect_AndIsDiscarded()
    {
        var game = Fight("monster-l1", b => b.Hand(1, "loot-manual"));

        var result = game.Execute(new PlayCard("p1", IdOf(game, 1, "loot-manual")));

        Assert.True(result.Accepted);
        Assert.Contains(result.Events, e => e is ManualEffectShown { CardId: "loot-manual" });
        Assert.Empty(game.State.Players[0].Hand);
        Assert.Contains(game.State.LootDiscard, c => c.CardId == "loot-manual");
        Assert.Empty(game.State.Combat!.OneShots);
    }

    [Fact]
    public void Enhancer_RaisesMonsterAndLoot()
    {
        var game = Fight("monster-l3-legacy", b => b.Hand(2, "enhancer-plus5"));
        var before = StrengthCalculator.Summarize(game.State, game.Catalog);
        var monsterId = game.State.Combat!.Monsters[0].InstanceId;

        var result = game.Execute(new PlayCard("p2", IdOf(game, 2, "enhancer-plus5"), TargetMonsterInstanceId: monsterId));

        var after = StrengthCalculator.Summarize(game.State, game.Catalog);
        Assert.True(result.Accepted);
        Assert.Equal(before.MonsterStrength + 5, after.MonsterStrength);
        Assert.Equal(before.TotalRewardLoot + 1, after.TotalRewardLoot);
        Assert.Single(game.State.Combat.Enhancers[monsterId]);
    }

    [Fact]
    public void Enhancer_WithoutTarget_IsRejected()
    {
        var game = Fight("monster-l3-legacy", b => b.Hand(2, "enhancer-plus5"));

        Assert.False(game.Execute(new PlayCard("p2", IdOf(game, 2, "enhancer-plus5"))).Accepted);
        Assert.Single(game.State.Players[1].Hand);
    }

    [Fact]
    public void Enhancer_WithUnknownTarget_IsRejected()
    {
        var game = Fight("monster-l3-legacy", b => b.Hand(2, "enhancer-plus5"));

        Assert.False(game.Execute(new PlayCard("p2", IdOf(game, 2, "enhancer-plus5"), TargetMonsterInstanceId: 99999)).Accepted);
    }

    [Fact]
    public void WanderingBug_AddsSecondMonster_StrengthsAndRewardsSum()
    {
        var game = Fight("monster-l3-legacy", b => b.Hand(2, "special-wandering-bug", "monster-l1"));
        var before = StrengthCalculator.Summarize(game.State, game.Catalog);

        var result = game.Execute(new PlayCard("p2", IdOf(game, 2, "special-wandering-bug"), SecondaryInstanceId: IdOf(game, 2, "monster-l1")));

        var after = StrengthCalculator.Summarize(game.State, game.Catalog);
        Assert.True(result.Accepted);
        Assert.Equal(2, game.State.Combat!.Monsters.Count);
        Assert.Equal(before.MonsterStrength + 1, after.MonsterStrength);
        Assert.True(after.TotalRewardLevels > before.TotalRewardLevels);
        Assert.Equal(["special-wandering-bug"], game.State.Combat.OtherPlayed.Select(c => c.CardId));
        Assert.Empty(game.State.Players[1].Hand);
    }

    [Fact]
    public void WanderingBug_WithoutMonsterInHand_IsRejected()
    {
        var game = Fight("monster-l3-legacy", b => b.Hand(2, "special-wandering-bug", "equip-head"));

        var result = game.Execute(new PlayCard("p2", IdOf(game, 2, "special-wandering-bug"), SecondaryInstanceId: IdOf(game, 2, "equip-head")));

        Assert.False(result.Accepted);
        Assert.Single(game.State.Combat!.Monsters);
        Assert.Equal(2, game.State.Players[1].Hand.Count);
    }

    [Fact]
    public void LevelUp_InCombat_IsRejected()
    {
        var game = Fight("monster-l1", b => b.Hand(1, "levelup-raise"));

        Assert.False(game.Execute(new PlayCard("p1", IdOf(game, 1, "levelup-raise"))).Accepted);
        Assert.Single(game.State.Players[0].Hand);
    }

    [Fact]
    public void Equipment_InCombat_IsRejected()
    {
        var game = Fight("monster-l1", b => b.Hand(1, "equip-head"));

        Assert.False(game.Execute(new PlayCard("p1", IdOf(game, 1, "equip-head"))).Accepted);
    }

    [Fact]
    public void CardPlay_WhileFleeing_IsRejected()
    {
        var game = Fight("monster-l3-legacy", b => b.Hand(1, "oneshot-plus5"));
        game.Execute(new ResolveCombat("p1"));

        Assert.False(game.Execute(new PlayCard("p1", IdOf(game, 1, "oneshot-plus5"), Side: Side.Player)).Accepted);
    }
}
