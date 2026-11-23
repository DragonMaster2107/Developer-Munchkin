using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class ViewTests
{
    [Fact]
    public void PublicView_HidesAllHands()
    {
        var game = GameBuilder.Create().Build();

        var view = game.GetView(null);

        Assert.Null(view.MyHand);
        Assert.All(view.Players, p => Assert.Equal(8, p.HandCount));
        Assert.Equal(3, view.Players.Count);
    }

    [Fact]
    public void PlayerView_ShowsOnlyOwnHand()
    {
        var game = GameBuilder.Create().Build();

        var view = game.GetView("p2");

        Assert.Equal(game.State.Players[1].Hand.Select(c => c.InstanceId), view.MyHand!.Select(c => c.InstanceId));
        Assert.NotSame(game.State.Players[1].Hand, view.MyHand);
    }

    [Theory]
    [InlineData(1, "Unbezahlter Praktikant")]
    [InlineData(2, "Werkstudent")]
    [InlineData(3, "Junior Developer")]
    [InlineData(4, "Developer")]
    [InlineData(5, "Senior Developer")]
    [InlineData(6, "Senior Developer mit Meinung")]
    [InlineData(7, "Software Architect")]
    [InlineData(8, "Principal Engineer")]
    [InlineData(9, "Tech Lead")]
    [InlineData(10, "CTO")]
    public void LevelTitles_MatchSpec(int level, string title)
    {
        Assert.Equal(title, LevelTitles.For(level));
    }

    [Fact]
    public void View_IncludesCombatSummary_DuringCombat()
    {
        var game = GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop("monster-l1").Build();
        game.Execute(new OpenTicket("p1"));

        var view = game.GetView(null);

        Assert.Equal(GamePhase.Combat, view.Phase);
        Assert.Equal("p1", view.ActivePlayerId);
        Assert.NotNull(view.Combat);
        Assert.NotSame(game.State.Combat, view.Combat!.State);
        Assert.Equal(game.State.Combat!.Monsters.Select(m => m.InstanceId), view.Combat.State.Monsters.Select(m => m.InstanceId));
        Assert.Equal(StrengthCalculator.Summarize(game.State, game.Catalog), view.Combat.Summary);
        Assert.Equal(1, view.Players[0].Level);
        Assert.Equal("Unbezahlter Praktikant", view.Players[0].Title);
    }

    [Fact]
    public void CombatView_TakenBeforeResolve_DoesNotChangeAfterwards()
    {
        var game = GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop("monster-l1").Build();
        game.Execute(new OpenTicket("p1"));
        var view = game.GetView("p1");

        Assert.True(game.Execute(new ResolveCombat("p1")).Accepted);
        Assert.Equal(CombatStage.Fleeing, game.State.Combat!.Stage);

        Assert.Equal(CombatStage.Fighting, view.Combat!.State.Stage);
        Assert.Empty(view.Combat.State.Fleeing);
        Assert.Single(view.Combat.State.Monsters);
    }

    [Fact]
    public void ViewCards_AreCopies_NotLiveState()
    {
        var game = GameBuilder.Create().Equip(1, "equip-body").Build();
        var view = game.GetView("p1");

        view.Players[0].Equipped[0].CardId = "changed";
        view.MyHand![0].CardId = "changed";

        Assert.Equal("equip-body", game.State.Players[0].Equipped[0].CardId);
        Assert.NotEqual("changed", game.State.Players[0].Hand[0].CardId);
    }

    [Fact]
    public void GameState_IsNotPublic()
    {
        var property = typeof(Game).GetProperty("State", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.Null(property);
    }

    [Fact]
    public void View_WithoutCombat_HasNoCombatView()
    {
        Assert.Null(GameBuilder.Create().Build().GetView(null).Combat);
    }

    [Fact]
    public void RecentLog_IsCappedAt100()
    {
        var game = GameBuilder.Create().Build();
        for (var i = 0; i < 150; i++)
        {
            game.State.Log.Add(new LogEntry { Seq = 100 + i, Text = $"e{i}" });
        }

        var view = game.GetView(null);

        Assert.Equal(100, view.RecentLog.Count);
        Assert.Equal("e149", view.RecentLog[^1].Text);
        Assert.Equal("e50", view.RecentLog[0].Text);
    }
}
