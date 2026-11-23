using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class EscapeTests
{
    /// <summary>Spieler 1 (Stufe 1) verliert gegen das Monster; danach wird seine Stufe gesetzt.</summary>
    private static Game Fleeing(string monsterId, int levelAfterLoss = 1, Func<GameBuilder, GameBuilder>? setup = null)
    {
        var builder = GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop(monsterId);
        var game = (setup?.Invoke(builder) ?? builder).Build();
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
        Assert.True(game.Execute(new ResolveCombat("p1")).Accepted);
        Assert.Equal(CombatStage.Fleeing, game.State.Combat!.Stage);
        game.State.Players[0].Level = levelAfterLoss;
        return game;
    }

    [Fact]
    public void RollFiveOrSix_Escapes()
    {
        var game = Fleeing("monster-l1", 3);
        game.ForceNextRolls(5);

        var result = game.Execute(new AttemptEscape("p1"));

        Assert.True(result.Accepted);
        Assert.Contains(result.Events, e => e is EscapeRolled { Roll: 5, Escaped: true });
        Assert.DoesNotContain(result.Events, e => e is BadStuffApplied);
        Assert.Equal(3, game.State.Players[0].Level);
    }

    [Fact]
    public void RollFour_AppliesBadStuff()
    {
        var game = Fleeing("monster-l1", 3);
        game.ForceNextRolls(4);

        var result = game.Execute(new AttemptEscape("p1"));

        Assert.Contains(result.Events, e => e is EscapeRolled { Roll: 4, Escaped: false });
        Assert.Contains(result.Events, e => e is BadStuffApplied { PlayerId: "p1", MonsterCardId: "monster-l1" });
        Assert.Equal(2, game.State.Players[0].Level);
    }

    [Fact]
    public void EscapeBonus_TurnsFourIntoEscape()
    {
        var game = Fleeing("monster-l1", 3, b => b.Department(1, "dept-cloud"));
        game.ForceNextRolls(4);

        var result = game.Execute(new AttemptEscape("p1"));

        Assert.Contains(result.Events, e => e is EscapeRolled { Roll: 4, Bonus: 1, Escaped: true });
        Assert.Equal(3, game.State.Players[0].Level);
    }

    [Fact]
    public void NoPursuitBelowLevel_EscapesWithoutRoll()
    {
        var game = Fleeing("monster-boss-l12", 2);

        var result = game.Execute(new AttemptEscape("p1"));

        Assert.Contains(result.Events, e => e is EscapeRolled { Roll: 0, Escaped: true });
        Assert.DoesNotContain(result.Events, e => e is BadStuffApplied);
        Assert.Equal(2, game.State.Players[0].Level);
    }

    [Fact]
    public void TwoMonsters_RollSeparately()
    {
        var game = Fleeing("monster-l1", 5);
        var extra = game.State.TicketDeck.First(x => x.CardId == "monster-l1");
        game.State.TicketDeck.Remove(extra);
        game.State.Combat!.Monsters.Add(extra);
        game.State.Combat.Fleeing[0].UnresolvedMonsterIds.Add(extra.InstanceId);
        game.ForceNextRolls(6, 1);

        var result = game.Execute(new AttemptEscape("p1"));

        Assert.Equal(2, result.Events.OfType<EscapeRolled>().Count());
        Assert.Single(result.Events.OfType<BadStuffApplied>());
        Assert.Equal(4, game.State.Players[0].Level);
    }

    [Fact]
    public void HelperFleesSeparately()
    {
        var game = GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop("monster-l3-legacy").Build();
        game.Execute(new OpenTicket("p1"));
        game.Execute(new OfferHelp("p2", 0));
        game.Execute(new AcceptHelp("p1", "p2"));
        // Monster verstärken, damit der Kampf verloren geht.
        var enhancer = game.State.TicketDeck.First(x => x.CardId == "enhancer-plus5");
        game.State.TicketDeck.Remove(enhancer);
        game.State.Combat!.Enhancers[game.State.Combat.Monsters[0].InstanceId] = [enhancer];
        Assert.True(game.Execute(new ResolveCombat("p1")).Accepted);
        Assert.Equal(2, game.State.Combat!.Fleeing.Count);
        game.State.Players[0].Level = 4;
        game.State.Players[1].Level = 4;

        game.ForceNextRolls(6);
        Assert.True(game.Execute(new AttemptEscape("p1")).Accepted);
        Assert.Equal("p2", Assert.Single(game.State.Combat!.Fleeing).PlayerId);

        game.ForceNextRolls(1);
        var second = game.Execute(new AttemptEscape("p2"));

        Assert.Contains(second.Events, e => e is BadStuffApplied { PlayerId: "p2" });
        Assert.Equal(4, game.State.Players[0].Level);
        Assert.Null(game.State.Combat);
    }

    [Fact]
    public void AfterAllFled_CombatClearedAndEndTurn()
    {
        var game = Fleeing("monster-l1", 3);
        game.ForceNextRolls(6);

        game.Execute(new AttemptEscape("p1"));

        Assert.Null(game.State.Combat);
        Assert.Equal(GamePhase.EndTurn, game.State.Phase);
        Assert.Contains(game.State.TicketDiscard, x => x.CardId == "monster-l1");
    }

    [Fact]
    public void BadStuffWithChoice_LeavesPendingChoice_CombatStillCleared()
    {
        var game = Fleeing("monster-l8-hates-qa", 1, b => b.Equip(1, "equip-small"));
        game.ForceNextRolls(1);

        var result = game.Execute(new AttemptEscape("p1"));

        Assert.True(result.Accepted);
        Assert.Single(game.State.PendingChoices);
        Assert.Equal(ChoiceKind.DiscardSmallItem, game.State.PendingChoices[0].Kind);
        Assert.Null(game.State.Combat);
        Assert.Equal(GamePhase.EndTurn, game.State.Phase);
    }

    [Fact]
    public void RemoteDay_WhileFleeing_EscapesAll()
    {
        var game = Fleeing("monster-l1", 3, b => b.Hand(1, "oneshot-remote"));
        var card = game.State.Players[0].Hand.Single();

        var result = game.Execute(new PlayCard("p1", card.InstanceId));

        Assert.True(result.Accepted);
        Assert.Contains(result.Events, e => e is EscapeRolled { Escaped: true });
        Assert.Empty(game.State.Players[0].Hand);
        Assert.Contains(game.State.LootDiscard, x => x.InstanceId == card.InstanceId);
        Assert.Null(game.State.Combat);
        Assert.Equal(GamePhase.EndTurn, game.State.Phase);
        Assert.Equal(3, game.State.Players[0].Level);
    }

    [Fact]
    public void RemoteDay_WhileFighting_StillRejected()
    {
        var game = GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop("monster-l1").Hand(1, "oneshot-remote").Build();
        game.Execute(new OpenTicket("p1"));

        Assert.False(game.Execute(new PlayCard("p1", game.State.Players[0].Hand.Single().InstanceId)).Accepted);
    }

    [Fact]
    public void AttemptEscape_ByNonFleeingPlayer_IsRejected()
    {
        var game = Fleeing("monster-l1", 3);
        game.ForceNextRolls(1);
        var before = game.Save();

        var result = game.Execute(new AttemptEscape("p2"));

        Assert.False(result.Accepted);
        Assert.Equal(before, game.Save());
    }

    [Fact]
    public void SaveLoad_MidFleeing_ContinuesIdentically()
    {
        var game = Fleeing("monster-l1", 3);
        var json = game.Save();
        var loaded = Game.Load(json, TestData.Catalog);

        var a = game.Execute(new AttemptEscape("p1"));
        var b = loaded.Execute(new AttemptEscape("p1"));

        Assert.True(a.Accepted);
        Assert.Equal(a.Events, b.Events);
        Assert.Equal(game.Save(), loaded.Save());
    }

    [Fact]
    public void AttemptEscape_SkipsMonsterIdsNoLongerInCombat()
    {
        var game = Fleeing("monster-l1", 3);
        game.State.Combat!.Fleeing[0].UnresolvedMonsterIds.Add(99999);
        game.ForceNextRolls(6);

        var result = game.Execute(new AttemptEscape("p1"));

        Assert.True(result.Accepted);
        Assert.Single(result.Events.OfType<EscapeRolled>());
        Assert.Null(game.State.Combat);
    }

    [Fact]
    public void RemoteDay_WithNothingLeftToEscape_IsRejectedAndKeepsCard()
    {
        var game = Fleeing("monster-l1", 3, b => b.Hand(1, "oneshot-remote"));
        game.State.Combat!.Fleeing[0].UnresolvedMonsterIds.Clear();
        var card = game.State.Players[0].Hand.Single();

        var result = game.Execute(new PlayCard("p1", card.InstanceId));

        Assert.False(result.Accepted);
        Assert.Equal("Du musst gerade nicht fliehen.", result.Rejection);
        Assert.Contains(game.State.Players[0].Hand, x => x.InstanceId == card.InstanceId);
    }

    [Fact]
    public void ResolveCombat_LostWithoutMonsters_CreatesNoFleeingEntries()
    {
        var game = GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop("monster-l1").Build();
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
        var monster = game.State.Combat!.Monsters[0];
        game.State.Combat.Monsters.Clear();
        game.State.TicketDiscard.Add(monster);
        game.State.Combat.ManualMonsterAdjust = 10;

        var result = game.Execute(new ResolveCombat("p1"));

        Assert.True(result.Accepted);
        Assert.Null(game.State.Combat);
        Assert.Equal(GamePhase.EndTurn, game.State.Phase);
    }
}
