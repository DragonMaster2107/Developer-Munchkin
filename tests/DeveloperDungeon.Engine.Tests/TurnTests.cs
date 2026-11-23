using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class TurnTests
{
    private static GameBuilder Turn(GamePhase phase = GamePhase.OpenTicket) =>
        GameBuilder.Create().ClearHands().Active(1).Phase(phase);

    [Fact]
    public void OpenTicket_Monster_StartsCombat()
    {
        var game = Turn().TicketTop("monster-l1").Build();

        var result = game.Execute(new OpenTicket("p1"));

        Assert.True(result.Accepted);
        Assert.Equal(GamePhase.Combat, game.State.Phase);
        Assert.Equal(["monster-l1"], game.State.Combat!.Monsters.Select(m => m.CardId));
        Assert.Equal(CombatStage.Fighting, game.State.Combat.Stage);
        Assert.Contains(result.Events, e => e is TicketOpened { PlayerId: "p1", CardId: "monster-l1" });
        Assert.Contains(result.Events, e => e is CombatStarted { PlayerId: "p1", MonsterCardId: "monster-l1" });
    }

    [Fact]
    public void OpenTicket_Curse_AppliesToActivePlayer_ThenAfterTicket()
    {
        var game = Turn().TicketTop("curse-lose-level").Level(1, 4).Build();

        var result = game.Execute(new OpenTicket("p1"));

        Assert.True(result.Accepted);
        Assert.Equal(3, game.State.Players[0].Level);
        Assert.Equal(GamePhase.AfterTicket, game.State.Phase);
        Assert.Contains(result.Events, e => e is CursePlayed { ById: "p1", TargetId: "p1" });
    }

    [Fact]
    public void OpenTicket_OtherCard_GoesToHand()
    {
        var game = Turn().TicketTop("dept-qa").Build();

        var result = game.Execute(new OpenTicket("p1"));

        Assert.True(result.Accepted);
        Assert.Equal(["dept-qa"], game.State.Players[0].Hand.Select(c => c.CardId));
        Assert.Equal(GamePhase.AfterTicket, game.State.Phase);
    }

    [Fact]
    public void OpenTicket_ByNonActivePlayer_IsRejected()
    {
        var game = Turn().TicketTop("dept-qa").Build();

        var result = game.Execute(new OpenTicket("p2"));

        Assert.False(result.Accepted);
        Assert.Equal(GamePhase.OpenTicket, game.State.Phase);
        Assert.Empty(game.State.Players[1].Hand);
    }

    [Fact]
    public void OpenTicket_Twice_IsRejected()
    {
        var game = Turn().TicketTop("dept-qa", "dept-cloud").Build();
        game.Execute(new OpenTicket("p1"));

        var result = game.Execute(new OpenTicket("p1"));

        Assert.False(result.Accepted);
        Assert.Single(game.State.Players[0].Hand);
    }

    [Fact]
    public void OpenTicket_WithEmptyTicketDecks_GoesToAfterTicket()
    {
        var game = Turn().Build();
        game.State.TicketDeck.Clear();
        game.State.TicketDiscard.Clear();

        var result = game.Execute(new OpenTicket("p1"));

        Assert.True(result.Accepted);
        Assert.Equal(GamePhase.AfterTicket, game.State.Phase);
        Assert.Empty(game.State.Players[0].Hand);
    }

    [Fact]
    public void LookForTrouble_WithMonsterFromHand_StartsCombat()
    {
        var game = Turn(GamePhase.AfterTicket).Hand(1, "monster-l3-legacy").Build();
        var id = game.State.Players[0].Hand[0].InstanceId;

        var result = game.Execute(new LookForTrouble("p1", id));

        Assert.True(result.Accepted);
        Assert.Empty(game.State.Players[0].Hand);
        Assert.Equal(GamePhase.Combat, game.State.Phase);
        Assert.Equal(id, game.State.Combat!.Monsters[0].InstanceId);
        Assert.Contains(result.Events, e => e is CombatStarted { PlayerId: "p1", MonsterCardId: "monster-l3-legacy" });
    }

    [Fact]
    public void LookForTrouble_WithNonMonster_IsRejected()
    {
        var game = Turn(GamePhase.AfterTicket).Hand(1, "equip-head").Build();
        var id = game.State.Players[0].Hand[0].InstanceId;

        var result = game.Execute(new LookForTrouble("p1", id));

        Assert.False(result.Accepted);
        Assert.Single(game.State.Players[0].Hand);
        Assert.Equal(GamePhase.AfterTicket, game.State.Phase);
    }

    [Fact]
    public void WorkBacklog_DrawsTicketToHand_AndGoesToEndTurn()
    {
        var game = Turn(GamePhase.AfterTicket).TicketTop("monster-l1").Build();

        var result = game.Execute(new WorkBacklog("p1"));

        Assert.True(result.Accepted);
        Assert.Equal(["monster-l1"], game.State.Players[0].Hand.Select(c => c.CardId));
        Assert.Equal(GamePhase.EndTurn, game.State.Phase);
        Assert.Contains(result.Events, e => e is CardDrawn { PlayerId: "p1", Deck: Engine.Cards.Deck.Ticket });
    }

    [Fact]
    public void EndTurn_FromAfterTicket_IsRejected()
    {
        var game = Turn(GamePhase.AfterTicket).Build();

        var result = game.Execute(new EndTurn("p1", []));

        Assert.False(result.Accepted);
        Assert.Equal(0, game.State.ActivePlayerIndex);
    }

    [Fact]
    public void EndTurn_PassesToNextPlayer_AndIncrementsTurn()
    {
        var game = Turn(GamePhase.EndTurn).Active(3).Build();
        game.State.TurnNumber = 4;

        var result = game.Execute(new EndTurn("p3", []));

        Assert.True(result.Accepted);
        Assert.Equal(0, game.State.ActivePlayerIndex);
        Assert.Equal(5, game.State.TurnNumber);
        Assert.Equal(GamePhase.OpenTicket, game.State.Phase);
        Assert.Contains(result.Events, e => e is TurnEnded { PlayerId: "p3", NextPlayerId: "p1" });
    }

    [Fact]
    public void EndTurn_ExpiresActiveTemporaryEffects()
    {
        var game = Turn(GamePhase.EndTurn).Build();
        game.State.TurnNumber = 3;
        game.State.TemporaryEffects.Add(new TemporaryEffect { PlayerId = "p1", CombatModifier = -2, StartsAfterTurnNumber = 1 });

        game.Execute(new EndTurn("p1", []));

        Assert.Empty(game.State.TemporaryEffects);
    }
}
