using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class CurseTests
{
    private static int IdOf(IEnumerable<CardInstance> cards, string cardId) =>
        cards.First(c => c.CardId == cardId).InstanceId;

    private static GameBuilder Turn() =>
        GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket);

    [Fact]
    public void LoseLevelCurse_FromHand_LowersTargetLevel_AndDiscards()
    {
        var game = Turn().Hand(1, "curse-lose-level").Level(2, 4).Build();
        var id = IdOf(game.State.Players[0].Hand, "curse-lose-level");

        var result = game.Execute(new PlayCard("p1", id, TargetPlayerId: "p2"));

        Assert.True(result.Accepted);
        Assert.Equal(3, game.State.Players[1].Level);
        Assert.Empty(game.State.Players[0].Hand);
        Assert.Contains(game.State.TicketDiscard, c => c.InstanceId == id);
        Assert.Contains(result.Events, e => e is CursePlayed { ById: "p1", TargetId: "p2" });
        Assert.Contains(result.Events, e => e is LevelChanged { PlayerId: "p2", From: 4, To: 3 });
    }

    [Fact]
    public void LoseLevels_NeverBelowOne()
    {
        var game = Turn().Level(1, 2).Build();

        EffectApplier.ApplyToPlayer(game.State, game.Catalog, game.Rng, "p1", [new LoseLevels(2)], "x", []);

        Assert.Equal(1, game.State.Players[0].Level);
    }

    [Fact]
    public void GainLevels_NeverReachesTen()
    {
        var game = Turn().Level(1, 8).Build();

        EffectApplier.ApplyToPlayer(game.State, game.Catalog, game.Rng, "p1", [new GainLevels(3)], "x", []);

        Assert.Equal(9, game.State.Players[0].Level);
    }

    [Fact]
    public void CurseWithoutTarget_IsRejected()
    {
        var game = Turn().Hand(1, "curse-lose-level").Build();
        var id = IdOf(game.State.Players[0].Hand, "curse-lose-level");

        var result = game.Execute(new PlayCard("p1", id));

        Assert.False(result.Accepted);
        Assert.Single(game.State.Players[0].Hand);
    }

    [Fact]
    public void Curse_InOtherPlayersTurn_IsAllowed()
    {
        var game = Turn().Hand(2, "curse-lose-level").Level(3, 5).Build();
        var id = IdOf(game.State.Players[1].Hand, "curse-lose-level");

        var result = game.Execute(new PlayCard("p2", id, TargetPlayerId: "p3"));

        Assert.True(result.Accepted);
        Assert.Equal(4, game.State.Players[2].Level);
    }

    [Fact]
    public void Curse_InSetup_IsRejected()
    {
        var game = GameBuilder.Create().ClearHands().Hand(1, "curse-lose-level").Build();
        var id = IdOf(game.State.Players[0].Hand, "curse-lose-level");

        var result = game.Execute(new PlayCard("p1", id, TargetPlayerId: "p2"));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void LoseHeadCurse_DiscardsHeadItem()
    {
        var game = Turn().Hand(1, "curse-lose-head").Equip(2, "equip-head", "equip-body").Build();
        var id = IdOf(game.State.Players[0].Hand, "curse-lose-head");

        var result = game.Execute(new PlayCard("p1", id, TargetPlayerId: "p2"));

        Assert.True(result.Accepted);
        Assert.Equal(["equip-body"], game.State.Players[1].Equipped.Select(c => c.CardId));
        Assert.Contains(game.State.LootDiscard, c => c.CardId == "equip-head");
        Assert.Contains(result.Events, e => e is CardsLost { PlayerId: "p2" });
    }

    [Fact]
    public void DiscardTwoCurse_RemovesTwoHandCards()
    {
        var game = Turn().Hand(1, "curse-discard-two")
            .Hand(2, "equip-head", "equip-body", "equip-feet").Build();
        var id = IdOf(game.State.Players[0].Hand, "curse-discard-two");

        var result = game.Execute(new PlayCard("p1", id, TargetPlayerId: "p2"));

        Assert.True(result.Accepted);
        Assert.Single(game.State.Players[1].Hand);
        Assert.Equal(2, game.State.LootDiscard.Count);
    }

    [Fact]
    public void DiscardHand_WithCountAtLeastHandSize_DiscardsWholeHand()
    {
        var game = Turn().Hand(2, "equip-head").Build();

        EffectApplier.ApplyToPlayer(game.State, game.Catalog, game.Rng, "p2", [new DiscardHand(5)], "x", []);

        Assert.Empty(game.State.Players[1].Hand);
    }

    [Fact]
    public void TeamAssistant_IsImmuneToSecurityCurse()
    {
        var game = Turn().Hand(1, "curse-give-item")
            .Department(2, "dept-team-assistant").Equip(2, "equip-body").Build();
        var id = IdOf(game.State.Players[0].Hand, "curse-give-item");

        var result = game.Execute(new PlayCard("p1", id, TargetPlayerId: "p2"));

        Assert.True(result.Accepted);
        Assert.Contains(result.Events, e => e is CurseBlocked { TargetId: "p2", CardId: "curse-give-item" });
        Assert.Empty(game.State.PendingChoices);
        Assert.Single(game.State.Players[1].Equipped);
        Assert.Contains(game.State.TicketDiscard, c => c.InstanceId == id);
    }

    [Fact]
    public void PenaltyCurse_StartsNextTurn()
    {
        var game = Turn().Hand(1, "curse-penalty").Build();
        var id = IdOf(game.State.Players[0].Hand, "curse-penalty");

        game.Execute(new PlayCard("p1", id, TargetPlayerId: "p2"));

        Assert.Equal(0, TemporaryEffects.CombatModifierFor(game.State, "p2"));
        game.State.TurnNumber++;
        Assert.Equal(-2, TemporaryEffects.CombatModifierFor(game.State, "p2"));
        TemporaryEffects.ExpireAtEndOfTurn(game.State, "p2");
        Assert.Equal(0, TemporaryEffects.CombatModifierFor(game.State, "p2"));
        Assert.Empty(game.State.TemporaryEffects);
    }

    [Fact]
    public void ExpireAtEndOfTurn_KeepsNotYetActiveEffects()
    {
        var game = Turn().Build();
        game.State.TemporaryEffects.Add(new TemporaryEffect { PlayerId = "p2", CombatModifier = -1, StartsAfterTurnNumber = 0 });

        TemporaryEffects.ExpireAtEndOfTurn(game.State, "p2");

        Assert.Single(game.State.TemporaryEffects);
    }

    [Fact]
    public void ManualCurse_EmitsManualEffectShown()
    {
        var game = Turn().Hand(1, "curse-manual").Build();
        var id = IdOf(game.State.Players[0].Hand, "curse-manual");

        var result = game.Execute(new PlayCard("p1", id, TargetPlayerId: "p2"));

        Assert.True(result.Accepted);
        Assert.Contains(result.Events, e => e is ManualEffectShown { CardId: "curse-manual" });
    }

    [Fact]
    public void PlayCard_NotInHand_IsRejected()
    {
        var game = Turn().Hand(2, "curse-lose-level").Build();
        var id = IdOf(game.State.Players[1].Hand, "curse-lose-level");

        var result = game.Execute(new PlayCard("p1", id, TargetPlayerId: "p2"));

        Assert.False(result.Accepted);
    }

    private static GameBuilder ExtendedTurn() =>
        GameBuilder.Create(catalog: TestData.ExtendedCatalog).ClearHands().Active(1).Phase(GamePhase.OpenTicket);

    [Fact]
    public void GiveThenLoseHands_OnlyHandItem_LeavesNoUnresolvableChoice()
    {
        var game = ExtendedTurn().Hand(1, "curse-give-then-lose-hands").Equip(2, "equip-ide-1h").Build();
        var id = IdOf(game.State.Players[0].Hand, "curse-give-then-lose-hands");

        var result = game.Execute(new PlayCard("p1", id, TargetPlayerId: "p2"));

        Assert.True(result.Accepted);
        Assert.Empty(game.State.Players[1].Equipped);
        Assert.Empty(game.State.PendingChoices);
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
    }

    [Fact]
    public void CurseLog_NamesCaster()
    {
        var game = Turn().Hand(1, "curse-lose-level").Level(2, 3).Build();
        var id = IdOf(game.State.Players[0].Hand, "curse-lose-level");

        game.Execute(new PlayCard("p1", id, TargetPlayerId: "p2"));

        Assert.Contains(game.State.Log, l => l.Text == "Spieler 1 verflucht Spieler 2 mit curse-lose-level.");
    }

    [Fact]
    public void DrawCardsEffect_EmitsCardDrawnAndLogs()
    {
        var game = ExtendedTurn().Hand(1, "curse-draw-two").Build();
        var id = IdOf(game.State.Players[0].Hand, "curse-draw-two");

        var result = game.Execute(new PlayCard("p1", id, TargetPlayerId: "p2"));

        Assert.True(result.Accepted);
        Assert.Equal(2, game.State.Players[1].Hand.Count);
        Assert.Equal(2, result.Events.OfType<CardDrawn>().Count(e => e is { PlayerId: "p2", Deck: Deck.Loot }));
        Assert.Contains(game.State.Log, l => l.Text == "Spieler 2 zieht 2 Karten.");
    }
}
