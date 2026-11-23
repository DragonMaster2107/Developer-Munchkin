using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class ToolboxTests
{
    private static GameBuilder Turn() =>
        GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket);

    private static int IdOf(IEnumerable<CardInstance> cards, string cardId) =>
        cards.First(c => c.CardId == cardId).InstanceId;

    private static Game InCombat()
    {
        var game = Turn().TicketTop("monster-l1").Build();
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
        return game;
    }

    private static Game WithGiveChoice(params string[] p2Equipment)
    {
        var game = Turn().Hand(1, "curse-give-item").Equip(2, p2Equipment).Build();
        var curse = IdOf(game.State.Players[0].Hand, "curse-give-item");
        Assert.True(game.Execute(new PlayCard("p1", curse, TargetPlayerId: "p2")).Accepted);
        Assert.Single(game.State.PendingChoices);
        return game;
    }

    [Fact]
    public void AdjustLevel_ClampsToOne()
    {
        var game = Turn().Level(2, 2).Build();

        var result = game.Execute(new ManualAdjustLevel("p1", "p2", -5));

        Assert.True(result.Accepted);
        Assert.Equal(1, game.State.Players[1].Level);
        Assert.Contains(result.Events, e => e is ToolboxUsed);
    }

    [Fact]
    public void AdjustLevel_ToTen_EndsGame()
    {
        var game = Turn().Level(2, 8).Build();

        var result = game.Execute(new ManualAdjustLevel("p1", "p2", 5));

        Assert.True(result.Accepted);
        Assert.Equal(10, game.State.Players[1].Level);
        Assert.Equal(GamePhase.GameOver, game.State.Phase);
        Assert.Equal("p2", game.State.WinnerId);
        Assert.Contains(result.Events, e => e is GameWon { PlayerId: "p2" });
    }

    [Fact]
    public void AdjustLevel_UnknownTarget_IsRejected()
    {
        var game = Turn().Build();
        var logCount = game.State.Log.Count;

        var result = game.Execute(new ManualAdjustLevel("p1", "nobody", 1));

        Assert.False(result.Accepted);
        Assert.Equal(logCount, game.State.Log.Count);
    }

    [Fact]
    public void MoveCard_FromOtherPlayersEquipmentToMyHand()
    {
        var game = Turn().Equip(2, "equip-body").Build();
        var id = IdOf(game.State.Players[1].Equipped, "equip-body");

        var result = game.Execute(new ManualMoveCard("p1", id, "p1", Zone.Hand));

        Assert.True(result.Accepted);
        Assert.Empty(game.State.Players[1].Equipped);
        Assert.Contains(game.State.Players[0].Hand, c => c.InstanceId == id);
    }

    [Fact]
    public void MoveCard_FromCombat_AndFromDiscard()
    {
        var game = InCombat();
        var monsterId = game.State.Combat!.Monsters[0].InstanceId;

        Assert.True(game.Execute(new ManualMoveCard("p1", monsterId, "p2", Zone.Backpack)).Accepted);
        Assert.Null(game.State.Combat);
        Assert.Contains(game.State.Players[1].Backpack, c => c.InstanceId == monsterId);

        var card = game.State.LootDeck[0];
        game.State.LootDeck.RemoveAt(0);
        game.State.LootDiscard.Add(card);
        Assert.True(game.Execute(new ManualMoveCard("p1", card.InstanceId, "p3", Zone.Equipped)).Accepted);
        Assert.Contains(game.State.Players[2].Equipped, c => c.InstanceId == card.InstanceId);
        Assert.DoesNotContain(game.State.LootDiscard, c => c.InstanceId == card.InstanceId);
    }

    [Theory]
    [InlineData(Zone.Hand, "Hand")]
    [InlineData(Zone.Equipped, "Ausrüstung")]
    [InlineData(Zone.Backpack, "Rucksack")]
    [InlineData(Zone.Department, "Abteilung")]
    [InlineData(Zone.Language, "Sprache")]
    public void MoveCard_LogsGermanZoneName(Zone zone, string german)
    {
        var game = Turn().Equip(2, "equip-body").Build();
        var id = IdOf(game.State.Players[1].Equipped, "equip-body");

        Assert.True(game.Execute(new ManualMoveCard("p1", id, "p3", zone)).Accepted);

        Assert.EndsWith($"equip-body wandert zu Spieler 3 ({german}).", game.State.Log[^1].Text);
    }

    [Fact]
    public void MoveCard_UnknownCardOrPlayer_IsRejected()
    {
        var game = Turn().Hand(1, "equip-body").Build();
        var id = game.State.Players[0].Hand[0].InstanceId;

        Assert.False(game.Execute(new ManualMoveCard("p1", 99999, "p1", Zone.Hand)).Accepted);
        Assert.False(game.Execute(new ManualMoveCard("p1", id, "nobody", Zone.Hand)).Accepted);
        Assert.Single(game.State.Players[0].Hand);
    }

    [Fact]
    public void Discard_FromCombat_RemovesMonster()
    {
        var game = InCombat();
        var monster = game.State.Combat!.Monsters[0];

        var result = game.Execute(new ManualDiscard("p2", monster.InstanceId));

        Assert.True(result.Accepted);
        Assert.Null(game.State.Combat);
        Assert.Contains(game.State.TicketDiscard, c => c.InstanceId == monster.InstanceId);
    }

    [Fact]
    public void Discard_FromPlayerZone_GoesToMatchingDiscard()
    {
        var game = Turn().Equip(2, "equip-body").Build();
        var id = game.State.Players[1].Equipped[0].InstanceId;

        Assert.True(game.Execute(new ManualDiscard("p1", id)).Accepted);

        Assert.Empty(game.State.Players[1].Equipped);
        Assert.Contains(game.State.LootDiscard, c => c.InstanceId == id);
    }

    [Fact]
    public void TakeFromDiscard_ToHand()
    {
        var game = Turn().Build();
        var card = game.State.LootDeck[0];
        game.State.LootDeck.RemoveAt(0);
        game.State.LootDiscard.Add(card);

        var result = game.Execute(new ManualTakeFromDiscard("p1", card.InstanceId, "p2"));

        Assert.True(result.Accepted);
        Assert.DoesNotContain(game.State.LootDiscard, c => c.InstanceId == card.InstanceId);
        Assert.Contains(game.State.Players[1].Hand, c => c.InstanceId == card.InstanceId);
    }

    [Fact]
    public void TakeFromDiscard_CardNotInDiscard_IsRejected()
    {
        var game = Turn().Hand(1, "equip-body").Build();

        var result = game.Execute(new ManualTakeFromDiscard("p1", game.State.Players[0].Hand[0].InstanceId, "p1"));

        Assert.False(result.Accepted);
    }

    private static (int Player, int Monster) StrengthOf(Game game)
    {
        var summary = StrengthCalculator.Summarize(game.State, game.Catalog);
        return (summary.PlayerStrength, summary.MonsterStrength);
    }

    [Fact]
    public void CombatAdjust_ChangesSummary()
    {
        var game = InCombat();
        var before = StrengthOf(game);

        Assert.True(game.Execute(new ManualCombatAdjust("p2", Side.Player, 3)).Accepted);
        Assert.True(game.Execute(new ManualCombatAdjust("p2", Side.Monster, 2)).Accepted);

        var after = StrengthOf(game);
        Assert.Equal(before.Player + 3, after.Player);
        Assert.Equal(before.Monster + 2, after.Monster);
    }

    [Fact]
    public void CombatAdjust_WithoutCombat_IsRejected()
    {
        var game = Turn().Build();

        var result = game.Execute(new ManualCombatAdjust("p1", Side.Player, 1));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void Toolbox_AllowedWhileChoicePending()
    {
        var game = WithGiveChoice("equip-body", "equip-head");

        var result = game.Execute(new ManualAdjustLevel("p1", "p1", 1));

        Assert.True(result.Accepted);
        Assert.Single(game.State.PendingChoices);
    }

    [Fact]
    public void Toolbox_EmptyingChoosersEquipment_DropsPendingChoice()
    {
        var game = WithGiveChoice("equip-body");
        var equipped = game.State.Players[1].Equipped[0].InstanceId;

        var result = game.Execute(new ManualDiscard("p1", equipped));

        Assert.True(result.Accepted);
        Assert.Empty(game.State.PendingChoices);
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
    }

    [Fact]
    public void Toolbox_AfterGameOver_IsRejected()
    {
        var game = Turn().Phase(GamePhase.GameOver).Build();

        Assert.False(game.Execute(new ManualAdjustLevel("p1", "p1", 1)).Accepted);
    }

    [Fact]
    public void RollDie_EmitsValueInRange()
    {
        var game = Turn().Build();

        var result = game.Execute(new RollDie("p3"));

        Assert.True(result.Accepted);
        var rolled = Assert.Single(result.Events.OfType<DieRolled>());
        Assert.Equal("p3", rolled.PlayerId);
        Assert.InRange(rolled.Roll, 1, 6);
    }

    [Fact]
    public void RollDie_UsesForcedRolls()
    {
        var game = Turn().Build();
        game.ForceNextRolls(4);

        var result = game.Execute(new RollDie("p1"));

        Assert.Contains(result.Events, e => e is DieRolled { Roll: 4 });
    }

    [Fact]
    public void ToolboxAction_IsLoggedWithPrefix()
    {
        var game = Turn().Build();
        var logCount = game.State.Log.Count;

        game.Execute(new ManualAdjustLevel("p1", "p2", 1));

        Assert.True(game.State.Log.Count > logCount);
        Assert.StartsWith("[Werkzeugkasten]", game.State.Log[logCount].Text);
    }

    private static Game FleeingFrom(params string[] monsterIds)
    {
        var game = Turn().TicketTop(monsterIds[0]).Build();
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
        foreach (var id in monsterIds.Skip(1))
        {
            var extra = game.State.TicketDeck.First(x => x.CardId == id);
            game.State.TicketDeck.Remove(extra);
            game.State.Combat!.Monsters.Add(extra);
        }
        Assert.True(game.Execute(new ResolveCombat("p1")).Accepted);
        Assert.Equal(CombatStage.Fleeing, game.State.Combat!.Stage);
        return game;
    }

    [Fact]
    public void Discard_OnlyMonsterWhileFleeing_CleansUpCombat()
    {
        var game = FleeingFrom("monster-l1");
        var monsterId = game.State.Combat!.Monsters[0].InstanceId;

        Assert.True(game.Execute(new ManualDiscard("p1", monsterId)).Accepted);

        Assert.Null(game.State.Combat);
        Assert.Equal(GamePhase.EndTurn, game.State.Phase);
        Assert.Contains(game.State.TicketDiscard, c => c.InstanceId == monsterId);
        Assert.True(game.Execute(new EndTurn("p1", [])).Accepted);
    }

    [Fact]
    public void MoveCard_OneOfTwoMonstersWhileFleeing_EscapeStillWorks()
    {
        var game = FleeingFrom("monster-l1", "monster-l3-legacy");
        var legacy = game.State.Combat!.Monsters.First(m => m.CardId == "monster-l3-legacy").InstanceId;

        Assert.True(game.Execute(new ManualMoveCard("p1", legacy, "p2", Zone.Hand)).Accepted);
        Assert.Equal([game.State.Combat!.Monsters[0].InstanceId], game.State.Combat.Fleeing[0].UnresolvedMonsterIds);

        game.ForceNextRolls(6);
        var result = game.Execute(new AttemptEscape("p1"));

        Assert.True(result.Accepted);
        Assert.Single(result.Events.OfType<EscapeRolled>());
        Assert.Null(game.State.Combat);
        Assert.Equal(GamePhase.EndTurn, game.State.Phase);
    }

    [Fact]
    public void MoveCard_OnlyMonsterWhileFleeingWithHelper_FinishesAllEntries()
    {
        var game = Turn().TicketTop("monster-l1").Build();
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
        Assert.True(game.Execute(new OfferHelp("p2", 0)).Accepted);
        Assert.True(game.Execute(new AcceptHelp("p1", "p2")).Accepted);
        Assert.True(game.Execute(new ManualCombatAdjust("p1", Side.Monster, 10)).Accepted);
        Assert.True(game.Execute(new ResolveCombat("p1")).Accepted);
        Assert.Equal(2, game.State.Combat!.Fleeing.Count);
        var monsterId = game.State.Combat.Monsters[0].InstanceId;

        Assert.True(game.Execute(new ManualMoveCard("p3", monsterId, "p3", Zone.Hand)).Accepted);

        Assert.Null(game.State.Combat);
        Assert.Equal(GamePhase.EndTurn, game.State.Phase);
        Assert.Contains(game.State.Players[2].Hand, c => c.InstanceId == monsterId);
    }

    [Fact]
    public void Discard_OnlyMonsterWhileFighting_EndsCombatWithoutReward()
    {
        var game = InCombat();
        var monsterId = game.State.Combat!.Monsters[0].InstanceId;
        var logCount = game.State.Log.Count;

        var result = game.Execute(new ManualDiscard("p2", monsterId));

        Assert.True(result.Accepted);
        Assert.Null(game.State.Combat);
        Assert.Equal(GamePhase.EndTurn, game.State.Phase);
        Assert.Equal(1, game.State.Players[0].Level);
        Assert.Empty(game.State.Players[0].Hand);
        Assert.Contains(game.State.Log.Skip(logCount), l => l.Text.Contains("Kampf endet"));
        Assert.True(game.Execute(new EndTurn("p1", [])).Accepted);
    }

    [Fact]
    public void Discard_OneOfTwoMonstersWhileFighting_CombatContinues()
    {
        var game = InCombat();
        var extra = game.State.TicketDeck.First(x => x.CardId == "monster-l1");
        game.State.TicketDeck.Remove(extra);
        game.State.Combat!.Monsters.Add(extra);

        Assert.True(game.Execute(new ManualDiscard("p2", extra.InstanceId)).Accepted);

        Assert.NotNull(game.State.Combat);
        Assert.Single(game.State.Combat!.Monsters);
        Assert.Equal(GamePhase.Combat, game.State.Phase);
    }
}
