using Microsoft.Extensions.DependencyInjection.Extensions;
using AngleSharp.Dom;
using Bunit;
using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Pages;
using DeveloperDungeon.Web.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DeveloperDungeon.Web.Tests.Components;

public class PlayerAreaTests : BunitContext
{
    private IRenderedComponent<Table> RenderTable(TableController controller)
    {
        var storage = new InMemoryGameStorage();
        Services.AddSingleton<IGameStorage>(storage);
        Services.AddSingleton(new SettingsStore(storage));
        Services.AddSingleton(TestGames.Catalog);
        Services.AddSingleton(CardArtCatalog.Parse("{}"));
        Services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        Services.AddSingleton(controller);
        return Render<Table>();
    }

    private static IElement Button(IRenderedComponent<Table> cut, string selector, string text) =>
        cut.FindAll($"{selector} button").First(b => b.TextContent.Trim() == text);

    private static IElement HandCard(IRenderedComponent<Table> cut, int instanceId) =>
        cut.Find($".hand-card[data-instance='{instanceId}'] .card");

    private static CardDefinition Def(string cardId) => TestGames.Catalog.Get(cardId);

    /// <summary>Sucht einen Seed, bei dem die aufgedeckte Hand des aktiven Spielers eine passende Karte enthält.</summary>
    private static async Task<TableController> WithHandAsync(Func<IReadOnlyList<CardDefinition>, bool> predicate)
    {
        for (ulong seed = 1; seed < 500; seed++)
        {
            var c = await TestGames.StartedAsync(3, seed);
            c.ConfirmHandoff();
            if (predicate([.. c.View.MyHand!.Select(x => Def(x.CardId))]))
            {
                return c;
            }
        }
        throw new InvalidOperationException("Kein passender Seed.");
    }

    [Fact]
    public async Task Curse_ChoosePlayer_SendsCommand()
    {
        var c = await WithHandAsync(h => h.Any(d => d.Kind == CardKind.Curse));
        var curse = c.View.MyHand!.First(x => Def(x.CardId).Kind == CardKind.Curse);
        var cut = RenderTable(c);
        var victim = c.View.Players.First(p => p.Id != c.ActorId);

        HandCard(cut, curse.InstanceId).Click();
        Button(cut, ".action-menu", "Verfluchen …").Click();
        Button(cut, ".target-picker", victim.Name).Click();

        cut.WaitForAssertion(() => Assert.Contains(c.View.RecentLog, e => e.Text.Contains(Def(curse.CardId).Name)));
        Assert.DoesNotContain(c.View.MyHand!, x => x.InstanceId == curse.InstanceId);
    }

    [Fact]
    public async Task IllegalAction_IsDisabledWithReason()
    {
        TableController? c = null;
        for (ulong seed = 1; seed < 500 && c is null; seed++)
        {
            var candidate = await TestGames.StartedAsync(3, seed);
            candidate.ConfirmHandoff();
            candidate.BeginInterjection(candidate.View.Players.First(p => p.Id != candidate.ActorId).Id);
            candidate.ConfirmHandoff();
            if (candidate.View.MyHand!.Any(x => Def(x.CardId).Kind == CardKind.Equipment))
            {
                c = candidate;
            }
        }
        Assert.NotNull(c);
        var equipment = c.View.MyHand!.First(x => Def(x.CardId).Kind == CardKind.Equipment);
        var rejection = (await c.CheckAsync(new EquipCard(c.ActorId, equipment.InstanceId))).Rejection;
        Assert.False(string.IsNullOrEmpty(rejection));
        var cut = RenderTable(c);

        HandCard(cut, equipment.InstanceId).Click();

        var equip = Button(cut, ".action-menu", "Anlegen");
        Assert.True(equip.HasAttribute("disabled"));
        Assert.Contains(rejection, cut.Find(".action-menu").TextContent);
    }

    [Fact]
    public async Task Sell_ShowsLiveSumAndSends()
    {
        var c = await WithHandAsync(h => h.Where(d => d.Value > 0).Sum(d => d.Value) >= 1000);
        var valued = c.View.MyHand!.Where(x => Def(x.CardId).Value > 0).ToList();
        var total = valued.Sum(x => Def(x.CardId).Value);
        var levels = total / 1000;
        var levelBefore = c.View.Players.First(p => p.Id == c.ActorId).Level;
        var cut = RenderTable(c);

        cut.FindAll(".player-area button").First(b => b.TextContent.Trim() == "Verkaufen").Click();
        Assert.Contains("0 Credits = 0 Stufen", cut.Find(".sell-sum").TextContent);
        foreach (var x in valued)
        {
            HandCard(cut, x.InstanceId).Click();
        }

        Assert.Contains($"{total} Credits = {levels} {(levels == 1 ? "Stufe" : "Stufen")}", cut.Find(".sell-sum").TextContent);

        Button(cut, ".sell-panel", "Verkaufen").Click();

        cut.WaitForAssertion(() =>
            Assert.Equal(levelBefore + levels, c.View.Players.First(p => p.Id == c.ActorId).Level));
        Assert.Empty(cut.FindAll(".sell-panel"));
    }

    [Fact]
    public async Task SellMode_DisablesCardsWithoutValue()
    {
        var c = await WithHandAsync(h => h.Any(d => d.Value <= 0) && h.Any(d => d.Value > 0));
        var worthless = c.View.MyHand!.First(x => Def(x.CardId).Value <= 0);
        var cut = RenderTable(c);

        cut.FindAll(".player-area button").First(b => b.TextContent.Trim() == "Verkaufen").Click();

        Assert.Equal("true", HandCard(cut, worthless.InstanceId).GetAttribute("aria-disabled"));
    }

    [Fact]
    public async Task CardCells_CarryScopeAttribute_SoScopedCssApplies()
    {
        var c = await WithHandAsync(h => h.Count > 0);
        var cut = RenderTable(c);

        string? Scope(IElement e) => e.GetAttributeNames().FirstOrDefault(a => a.StartsWith("b-"));
        var rootScope = Scope(cut.Find(".player-area"));

        Assert.NotNull(rootScope);
        Assert.NotNull(cut.Find(".hand-card").GetAttribute(rootScope));
        Assert.NotNull(cut.Find(".slot").GetAttribute(rootScope));
        Assert.NotNull(cut.Find(".slot-label").GetAttribute(rootScope));
    }
}
