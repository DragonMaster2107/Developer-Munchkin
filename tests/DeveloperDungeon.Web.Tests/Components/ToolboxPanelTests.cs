using AngleSharp.Dom;
using Bunit;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Web.Components;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DeveloperDungeon.Web.Tests.Components;

public class ToolboxPanelTests : BunitContext
{
    private IRenderedComponent<ToolboxPanel> RenderPanel(TableController controller)
    {
        Services.AddSingleton(TestGames.Catalog);
        Services.AddSingleton(CardArtCatalog.Parse("{}"));
        Services.AddSingleton(controller);
        return Render<ToolboxPanel>();
    }

    private static IElement ButtonIn(IRenderedComponent<ToolboxPanel> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    private static IElement Group(IRenderedComponent<ToolboxPanel> cut, string label) =>
        cut.FindAll(".toolbox-group").First(g => g.GetAttribute("aria-label") == label);

    private static async Task<TableController> StartedWithRevealedHandAsync()
    {
        var c = await TestGames.StartedAsync();
        c.ConfirmHandoff();
        return c;
    }

    [Fact]
    public async Task AdjustLevel_Plus_RaisesLevel()
    {
        var c = await StartedWithRevealedHandAsync();
        var target = c.View.Players[1];
        var cut = RenderPanel(c);

        cut.Find("select[aria-label='Spieler für Stufe']").Change(target.Id);
        ButtonIn(cut, "+1").Click();

        Assert.Equal(target.Level + 1, c.View.Players.First(p => p.Id == target.Id).Level);
    }

    [Fact]
    public async Task Discard_EquippedCardOfOtherPlayer_Works()
    {
        var c = await StartedWithRevealedHandAsync();
        var other = c.View.Players.First(p => p.Id != c.ActorId);
        await c.SendAsync(new ManualMoveCard(c.ActorId, 1, other.Id, Zone.Equipped));
        var cut = RenderPanel(c);

        Group(cut, $"Ausrüstung von {other.Name}").QuerySelector(".card")!.Click();
        ButtonIn(cut, "Karte abwerfen").Click();

        Assert.Empty(c.View.Players.First(p => p.Id == other.Id).Equipped);
        Assert.NotNull(c.View.TicketDiscardTop ?? c.View.LootDiscardTop);
    }

    [Fact]
    public async Task MoveCard_ToBackpack_Works()
    {
        var c = await StartedWithRevealedHandAsync();
        var from = c.View.Players[1];
        var to = c.View.Players[2];
        await c.SendAsync(new ManualMoveCard(c.ActorId, 1, from.Id, Zone.Equipped));
        var cut = RenderPanel(c);

        Group(cut, $"Ausrüstung von {from.Name}").QuerySelector(".card")!.Click();
        cut.Find("select[aria-label='Zielspieler']").Change(to.Id);
        cut.Find("select[aria-label='Zielzone']").Change(nameof(Zone.Backpack));
        ButtonIn(cut, "Karte verschieben").Click();

        Assert.Empty(c.View.Players.First(p => p.Id == from.Id).Equipped);
        Assert.Equal(1, Assert.Single(c.View.Players.First(p => p.Id == to.Id).Backpack).InstanceId);
    }

    [Fact]
    public async Task CombatAdjust_HiddenOutsideCombat()
    {
        var c = await StartedWithRevealedHandAsync();
        var cut = RenderPanel(c);

        Assert.DoesNotContain("Kampfstärke anpassen", cut.Markup);
    }

    [Fact]
    public async Task CombatAdjust_InCombat_ChangesPlayerStrength()
    {
        var (c, _) = await TestGames.InCombatAsync();
        var before = c.View.Combat!.Summary.PlayerStrength;
        var cut = RenderPanel(c);

        Assert.Contains("Kampfstärke anpassen", cut.Markup);
        ButtonIn(cut, "+1 Kampfstärke").Click();

        Assert.Equal(before + 1, c.View.Combat!.Summary.PlayerStrength);
    }

    [Fact]
    public async Task RollDie_ShowsResultBetweenOneAndSix()
    {
        var c = await StartedWithRevealedHandAsync();
        var cut = RenderPanel(c);

        ButtonIn(cut, "Würfeln").Click();

        var result = int.Parse(cut.Find(".die-result").TextContent.Trim());
        Assert.InRange(result, 1, 6);
    }

    [Fact]
    public async Task TakeFromDiscard_PutsTopCardIntoTargetHand()
    {
        var c = await StartedWithRevealedHandAsync();
        await c.SendAsync(new ManualDiscard(c.ActorId, c.View.MyHand![0].InstanceId));
        var top = (c.View.TicketDiscardTop ?? c.View.LootDiscardTop)!;
        var cut = RenderPanel(c);
        var label = c.View.TicketDiscardTop is not null ? "Oberste Ablage Tickets" : "Oberste Ablage Loot";

        Group(cut, label).QuerySelector(".card")!.Click();
        ButtonIn(cut, "Von der Ablage nehmen").Click();

        Assert.Contains(c.View.MyHand!, x => x.InstanceId == top.InstanceId);
    }

    [Fact]
    public async Task OtherPlayersHand_IsNotSelectable()
    {
        var c = await StartedWithRevealedHandAsync();
        var cut = RenderPanel(c);

        var hand = Group(cut, "Deine Hand");
        Assert.Equal(c.View.MyHand!.Count, hand.QuerySelectorAll(".card").Length);
        Assert.DoesNotContain(cut.FindAll(".toolbox-group"), g => (g.GetAttribute("aria-label") ?? "").Contains("Hand von"));
    }

    [Fact]
    public async Task OwnHand_NotOffered_WhenNotRevealed()
    {
        var c = await TestGames.StartedAsync();
        var cut = RenderPanel(c);

        Assert.DoesNotContain(cut.FindAll(".toolbox-group"), g => g.GetAttribute("aria-label") == "Deine Hand");
    }

    [Fact]
    public async Task ShowsHint()
    {
        var c = await StartedWithRevealedHandAsync();
        var cut = RenderPanel(c);

        Assert.Contains("Für Korrekturen und Hausregeln – alles landet im Protokoll.", cut.Markup);
    }
}
