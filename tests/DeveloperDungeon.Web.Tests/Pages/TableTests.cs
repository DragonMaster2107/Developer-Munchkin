using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using AngleSharp.Dom;
using Bunit;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Pages;
using DeveloperDungeon.Web.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DeveloperDungeon.Web.Tests.Pages;

public class TableTests : BunitContext
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

    private static IElement Button(IRenderedComponent<Table> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    [Fact]
    public void NoGame_RedirectsToStartPage()
    {
        var controller = new TableController(TestGames.Catalog, new InMemoryGameStorage(), new FakeTimeProvider());

        RenderTable(controller);

        Assert.EndsWith("/", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Handoff_IsShown_UntilRevealed()
    {
        var c = TestGames.NewController();
        var first = c.View.Players.First(p => p.Id == c.HandoffTargetId).Name;

        var cut = RenderTable(c);

        Assert.Contains($"Gib das Gerät an {first}", cut.Markup);
        Assert.Empty(cut.FindAll(".hand-area"));

        cut.FindAll("button").Single(b => b.TextContent.Contains("aufdecken")).Click();

        Assert.NotEmpty(cut.FindAll(".hand-area"));
        Assert.DoesNotContain("Gib das Gerät an", cut.Markup);
    }

    [Fact]
    public void SetupRound_FinishForAll_ShowsOpenTicket()
    {
        var c = TestGames.NewController();
        var cut = RenderTable(c);

        for (var i = 0; i < 3; i++)
        {
            cut.FindAll("button").Single(b => b.TextContent.Contains("aufdecken")).Click();
            Assert.Contains("Aufbau-Runde:", cut.Markup);
            Assert.DoesNotContain("Ticket öffnen", cut.Markup);
            Button(cut, "Fertig").Click();
        }

        cut.WaitForAssertion(() => Assert.Contains("aufdecken", cut.Markup));
        cut.FindAll("button").Single(b => b.TextContent.Contains("aufdecken")).Click();

        Assert.NotNull(Button(cut, "Ticket öffnen"));
        Assert.DoesNotContain("Aufbau-Runde:", cut.Markup);
    }

    [Fact]
    public async Task InterjectButton_HiddenInSetup_ShownDuringPlay()
    {
        var c = TestGames.NewController();
        c.ConfirmHandoff();
        var cut = RenderTable(c);
        Assert.DoesNotContain("Ich spiel was", cut.Markup);

        await TestGames.CompleteSetupAsync(c);
        c.ConfirmHandoff();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("button").Count(b => b.TextContent.Trim() == "Ich spiel was")));
    }

    [Fact]
    public async Task OpenTicket_Click_ShowsLogEntry()
    {
        var c = await TestGames.StartedAsync();
        c.ConfirmHandoff();
        var cut = RenderTable(c);
        var before = cut.FindAll(".log-entry").Count;

        Button(cut, "Ticket öffnen").Click();

        cut.WaitForAssertion(() => Assert.True(cut.FindAll(".log-entry").Count > before));
    }

    [Fact]
    public async Task RejectedCommand_ShowsToast()
    {
        var c = await TestGames.StartedAsync();
        c.ConfirmHandoff();
        var cut = RenderTable(c);

        var result = await cut.InvokeAsync(() => c.SendAsync(new WorkBacklog(c.View.ActivePlayerId)));

        Assert.False(result.Accepted);
        cut.WaitForAssertion(() => Assert.Contains(result.Rejection!, cut.Find(".toast").TextContent));
    }

    [Fact]
    public async Task EndTurn_WithSevenCards_RequiresSelectingExactlyTwo()
    {
        var c = await TestGames.AtEndTurnAsync(new InMemoryGameStorage());
        var active = c.View.ActivePlayerId;
        await HandSizing.SetHandSizeAsync(c, 7);
        Assert.Equal(7, c.View.MyHand!.Count);
        var cut = RenderTable(c);
        Assert.Contains("Wähle 2 Karten zum Abgeben", cut.Markup);

        void Toggle(int index) => cut.FindAll(".hand-card .card")[index].Click();
        bool Disabled() => Button(cut, "Feierabend").HasAttribute("disabled");

        Assert.True(Disabled());
        Toggle(0);
        Assert.True(Disabled());
        Toggle(1);
        Assert.False(Disabled());
        Toggle(2);
        Assert.True(Disabled());
        Toggle(2);
        Assert.Equal(2, cut.FindAll(".hand-card.selected").Count);

        Button(cut, "Feierabend").Click();

        cut.WaitForAssertion(() => Assert.Contains("Gib das Gerät an", cut.Markup));
        Assert.NotEqual(active, c.View.ActivePlayerId);
    }


    [Fact]
    public async Task EndTurn_SelectionDropsCardsThatLeftTheHand()
    {
        var c = await TestGames.AtEndTurnAsync(new InMemoryGameStorage());
        var active = c.View.ActivePlayerId;
        await HandSizing.SetHandSizeAsync(c, 7);
        var cut = RenderTable(c);
        var firstId = c.View.MyHand![0].InstanceId;

        cut.FindAll(".hand-card .card")[0].Click();
        cut.FindAll(".hand-card .card")[1].Click();
        Assert.False(Button(cut, "Feierabend").HasAttribute("disabled"));

        // Eine gewählte Karte verschwindet per Werkzeugkasten, die Hand hat danach 6 Karten, also ist 1 abzugeben.
        await cut.InvokeAsync(() => c.SendAsync(new ManualDiscard(active, firstId)));

        cut.WaitForAssertion(() => Assert.Contains("Wähle 1 Karten zum Abgeben", cut.Markup));
        Assert.Single(cut.FindAll(".hand-card.selected"));
        Assert.False(Button(cut, "Feierabend").HasAttribute("disabled"));

        // Auf 7 aufgefüllt braucht es wieder 2 gültige Karten; nur eine ist noch gewählt.
        await cut.InvokeAsync(() => HandSizing.SetHandSizeAsync(c, 7));
        cut.WaitForAssertion(() => Assert.Contains("Wähle 2 Karten zum Abgeben", cut.Markup));
        Assert.Single(cut.FindAll(".hand-card.selected"));
        Assert.True(Button(cut, "Feierabend").HasAttribute("disabled"));
    }

    [Fact]
    public async Task RejectedCommand_Twice_ShowsToastAgainAndAdvancesFeedbackVersion()
    {
        var c = await TestGames.StartedAsync();
        c.ConfirmHandoff();
        var cut = RenderTable(c);
        var bad = new WorkBacklog(c.View.ActivePlayerId);

        await cut.InvokeAsync(() => c.SendAsync(bad));
        var version = c.FeedbackVersion;
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".toast")));
        await cut.InvokeAsync(() => c.SendAsync(bad));

        Assert.Equal(version + 1, c.FeedbackVersion);
        Assert.Contains(c.LastRejection!, cut.Find(".toast").TextContent);
    }
    [Fact]
    public async Task Winner_ShowsVictoryScreen()
    {
        var c = await TestGames.StartedAsync();
        c.ConfirmHandoff();
        var active = c.View.ActivePlayerId;
        var name = c.View.Players.First(p => p.Id == active).Name;
        var cut = RenderTable(c);

        await cut.InvokeAsync(() => c.SendAsync(new ManualAdjustLevel(active, active, 9)));

        cut.WaitForAssertion(() => Assert.Contains($"{name} ist CTO!", cut.Markup));
        Button(cut, "Zur Startseite").Click();
        Assert.EndsWith("/", Services.GetRequiredService<NavigationManager>().Uri);
    }
}
