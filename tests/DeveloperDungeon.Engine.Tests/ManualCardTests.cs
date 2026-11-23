using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class ManualCardTests
{
    private static GameBuilder Turn(GamePhase phase = GamePhase.AfterTicket) =>
        GameBuilder.Create(catalog: TestData.CatalogWithManualCards).ClearHands().Active(1).Phase(phase);

    private static int IdOf(Game game, int player, string cardId) =>
        game.State.Players[player - 1].Hand.First(c => c.CardId == cardId).InstanceId;

    private static Game InCombat(Func<GameBuilder, GameBuilder> setup)
    {
        var game = setup(Turn(GamePhase.OpenTicket).TicketTop("monster-l1")).Build();
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
        return game;
    }

    private static void AssertPlayedManually(Game game, CommandResult result, string cardId, List<CardInstance> discard)
    {
        Assert.True(result.Accepted, result.Rejection);
        Assert.Contains(result.Events, e => e is ManualEffectShown { CardId: var id } && id == cardId);
        Assert.Contains(discard, c => c.CardId == cardId);
        Assert.Contains(game.State.Log, l => l.Text.Contains(cardId) && l.Text.Contains("manuelle Wirkung"));
    }

    [Fact]
    public void ManualCardsCatalog_IsValid() =>
        Assert.Empty(TestData.CatalogWithManualCards.Validate());

    [Fact]
    public void ManualSpecial_OnOwnTurnOutsideCombat_IsDiscardedAndShown()
    {
        var game = Turn().Hand(1, "special-manual").Build();

        var result = game.Execute(new PlayCard("p1", IdOf(game, 1, "special-manual")));

        AssertPlayedManually(game, result, "special-manual", game.State.TicketDiscard);
        Assert.Empty(game.State.Players[0].Hand);
    }

    [Fact]
    public void ManualSpecial_OnOtherPlayersTurnOutsideCombat_IsRejected()
    {
        var game = Turn().Hand(2, "special-manual").Build();
        var before = game.Save();

        var result = game.Execute(new PlayCard("p2", IdOf(game, 2, "special-manual")));

        Assert.False(result.Accepted);
        Assert.Equal(before, game.Save());
    }

    [Fact]
    public void ManualSpecial_InCombat_ByAnyPlayer()
    {
        var game = InCombat(b => b.Hand(2, "special-manual"));

        var result = game.Execute(new PlayCard("p2", IdOf(game, 2, "special-manual")));

        AssertPlayedManually(game, result, "special-manual", game.State.TicketDiscard);
        Assert.NotNull(game.State.Combat);
    }

    [Fact]
    public void ManualLevelUp_OnOwnTurn_DoesNotChangeLevel()
    {
        var game = Turn().Hand(1, "levelup-manual").Build();

        var result = game.Execute(new PlayCard("p1", IdOf(game, 1, "levelup-manual")));

        AssertPlayedManually(game, result, "levelup-manual", game.State.LootDiscard);
        Assert.Equal(1, game.State.Players[0].Level);
    }

    [Fact]
    public void ManualLevelUp_InCombat_ByOtherPlayer()
    {
        var game = InCombat(b => b.Hand(3, "levelup-manual"));

        var result = game.Execute(new PlayCard("p3", IdOf(game, 3, "levelup-manual")));

        AssertPlayedManually(game, result, "levelup-manual", game.State.LootDiscard);
    }

    [Fact]
    public void ManualLevelUp_OnOtherPlayersTurnOutsideCombat_IsRejected()
    {
        var game = Turn().Hand(2, "levelup-manual").Build();

        Assert.False(game.Execute(new PlayCard("p2", IdOf(game, 2, "levelup-manual"))).Accepted);
    }

    [Fact]
    public void ManualEnhancer_OutsideCombat_OnOtherPlayersTurn()
    {
        var game = Turn().Hand(2, "enhancer-manual").Build();

        var result = game.Execute(new PlayCard("p2", IdOf(game, 2, "enhancer-manual")));

        AssertPlayedManually(game, result, "enhancer-manual", game.State.TicketDiscard);
    }

    [Fact]
    public void ManualEnhancer_InCombat_NeedsNoTarget()
    {
        var game = InCombat(b => b.Hand(2, "enhancer-manual"));

        var result = game.Execute(new PlayCard("p2", IdOf(game, 2, "enhancer-manual")));

        AssertPlayedManually(game, result, "enhancer-manual", game.State.TicketDiscard);
        Assert.Empty(game.State.Combat!.Enhancers);
    }

    [Fact]
    public void ManualEnhancer_DuringSetup_IsRejected()
    {
        var game = Turn(GamePhase.Setup).Hand(1, "enhancer-manual").Build();

        Assert.False(game.Execute(new PlayCard("p1", IdOf(game, 1, "enhancer-manual"))).Accepted);
    }

    [Fact]
    public void ManualOneShot_OutsideCombat_OnOtherPlayersTurn()
    {
        var game = Turn().Hand(3, "loot-manual").Build();

        var result = game.Execute(new PlayCard("p3", IdOf(game, 3, "loot-manual")));

        AssertPlayedManually(game, result, "loot-manual", game.State.LootDiscard);
    }

    [Fact]
    public void ManualOneShot_InFight_StillReportsCombatPlay()
    {
        var game = InCombat(b => b.Hand(2, "loot-manual"));

        var result = game.Execute(new PlayCard("p2", IdOf(game, 2, "loot-manual")));

        AssertPlayedManually(game, result, "loot-manual", game.State.LootDiscard);
        Assert.Contains(result.Events, e => e is CardPlayedInCombat { PlayerId: "p2", CardId: "loot-manual" });
    }

    [Fact]
    public void AutomaticEnhancer_OutsideCombat_IsStillRejected()
    {
        var game = Turn().Hand(1, "enhancer-plus5").Build();

        Assert.False(game.Execute(new PlayCard("p1", IdOf(game, 1, "enhancer-plus5"))).Accepted);
    }
}
