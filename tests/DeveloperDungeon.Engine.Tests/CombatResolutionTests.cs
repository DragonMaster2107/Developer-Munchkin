using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class CombatResolutionTests
{
    private static Game Fight(string monster, Func<GameBuilder, GameBuilder>? setup = null)
    {
        var builder = GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop(monster);
        var game = (setup?.Invoke(builder) ?? builder).Build();
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
        return game;
    }

    [Fact]
    public void Win_GrantsLevelAndLoot_ThenEndTurn()
    {
        var game = Fight("monster-l3-legacy", b => b.Level(1, 4).LootTop("equip-head"));

        var result = game.Execute(new ResolveCombat("p1"));

        Assert.True(result.Accepted);
        Assert.Equal(5, game.State.Players[0].Level);
        Assert.Equal(["equip-head"], game.State.Players[0].Hand.Select(c => c.CardId));
        Assert.Equal(GamePhase.EndTurn, game.State.Phase);
        Assert.Null(game.State.Combat);
        Assert.Contains(game.State.TicketDiscard, c => c.CardId == "monster-l3-legacy");
        Assert.Contains(result.Events, e => e is CombatWon { PlayerId: "p1", LevelsGained: 1, LootDrawn: 1 });
    }

    [Fact]
    public void Tie_IsLoss_WithoutTiesWin()
    {
        var game = Fight("monster-l3-legacy", b => b.Level(1, 3));

        var result = game.Execute(new ResolveCombat("p1"));

        Assert.True(result.Accepted);
        Assert.Equal(CombatStage.Fleeing, game.State.Combat!.Stage);
        Assert.Contains(result.Events, e => e is CombatLost { PlayerId: "p1" });
        Assert.Equal(3, game.State.Players[0].Level);
    }

    [Fact]
    public void Tie_IsWin_WithQa()
    {
        var game = Fight("monster-l3-legacy", b => b.Level(1, 3).Department(1, "dept-qa"));

        game.Execute(new ResolveCombat("p1"));

        Assert.Equal(4, game.State.Players[0].Level);
        Assert.Equal(GamePhase.EndTurn, game.State.Phase);
    }

    [Fact]
    public void BossAtLevelNine_ReachesTenNotEleven_AndWins()
    {
        var game = Fight("monster-boss-l12", b => b.Level(1, 9).Equip(1, "equip-big", "equip-body", "equip-ide-2h").LootTop("equip-head", "equip-feet", "equip-small"));

        var result = game.Execute(new ResolveCombat("p1"));

        Assert.Equal(10, game.State.Players[0].Level);
        Assert.Equal(GamePhase.GameOver, game.State.Phase);
        Assert.Equal("p1", game.State.WinnerId);
        Assert.Contains(result.Events, e => e is GameWon { PlayerId: "p1" });
        Assert.Equal(3, game.State.Players[0].Hand.Count);
    }

    [Fact]
    public void Loss_EntersFleeing_WithActiveAndHelper()
    {
        var game = Fight("monster-l3-legacy");
        game.State.Combat!.HelperId = "p2";
        var monsterId = game.State.Combat.Monsters[0].InstanceId;

        game.Execute(new ResolveCombat("p1"));

        var combat = game.State.Combat!;
        Assert.Equal(CombatStage.Fleeing, combat.Stage);
        Assert.Equal(["p1", "p2"], combat.Fleeing.Select(f => f.PlayerId));
        Assert.All(combat.Fleeing, f => Assert.Equal([monsterId], f.UnresolvedMonsterIds));
        Assert.Equal(GamePhase.Combat, game.State.Phase);
    }

    [Fact]
    public void ResolveCombat_ByNonActive_IsRejected()
    {
        var game = Fight("monster-l1", b => b.Level(1, 4));

        var result = game.Execute(new ResolveCombat("p2"));

        Assert.False(result.Accepted);
        Assert.Equal(CombatStage.Fighting, game.State.Combat!.Stage);
        Assert.Equal(4, game.State.Players[0].Level);
    }

    [Fact]
    public void ResolveCombat_WhenAlreadyFleeing_IsRejected()
    {
        var game = Fight("monster-l3-legacy");
        game.Execute(new ResolveCombat("p1"));

        Assert.False(game.Execute(new ResolveCombat("p1")).Accepted);
    }

    [Fact]
    public void HelperGetsAgreedLootShare_AndNoLevel()
    {
        var game = Fight("monster-l8-hates-qa", b => b.Level(1, 5).Equip(1, "equip-big", "equip-body", "equip-ide-2h").LootTop("equip-head", "equip-feet"));
        game.State.Combat!.HelperId = "p2";
        game.State.Combat.HelperLootShare = 1;

        game.Execute(new ResolveCombat("p1"));

        Assert.Equal(6, game.State.Players[0].Level);
        Assert.Equal(1, game.State.Players[1].Level);
        Assert.Equal(["equip-head"], game.State.Players[1].Hand.Select(c => c.CardId));
        Assert.Equal(["equip-feet"], game.State.Players[0].Hand.Select(c => c.CardId));
    }

    [Fact]
    public void TeamAssistantHelper_GetsOneLevel()
    {
        var game = Fight("monster-l1", b => b.Level(1, 4).Department(2, "dept-team-assistant"));
        game.State.Combat!.HelperId = "p2";

        game.Execute(new ResolveCombat("p1"));

        Assert.Equal(2, game.State.Players[1].Level);
        Assert.Equal(5, game.State.Players[0].Level);
    }

    [Fact]
    public void TeamAssistantHelper_IsCappedAtNine()
    {
        var game = Fight("monster-l1", b => b.Level(1, 4).Level(2, 9).Department(2, "dept-team-assistant"));
        game.State.Combat!.HelperId = "p2";

        game.Execute(new ResolveCombat("p1"));

        Assert.Equal(9, game.State.Players[1].Level);
    }
}
