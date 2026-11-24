using AngleSharp.Dom;
using Bunit;
using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Web.Components;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Pages;
using DeveloperDungeon.Web.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace DeveloperDungeon.Web.Tests.Components;

public class CombatAreaTests : BunitContext
{
    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    private IRenderedComponent<Table> RenderTable(TableController controller)
    {
        var storage = new InMemoryGameStorage();
        Services.AddSingleton<IGameStorage>(storage);
        Services.AddSingleton(new SettingsStore(storage));
        Services.AddSingleton(TestGames.Catalog);
        Services.AddSingleton(CardArtCatalog.Parse("{}"));
        Services.AddSingleton(controller);
        return Render<Table>();
    }

    private static IElement Button(IRenderedComponent<Table> cut, string selector, string text) =>
        cut.FindAll($"{selector} button").First(b => b.TextContent.Trim() == text);

    private static string OtherPlayer(TableController c) => c.View.Players.First(p => p.Id != c.View.ActivePlayerId).Id;

    private static string NameOf(TableController c, string id) => c.View.Players.First(p => p.Id == id).Name;

    [Fact]
    public async Task ShowsStrengthComparison()
    {
        var (c, _) = await TestGames.InCombatAsync();
        var summary = c.View.Combat!.Summary;
        var monster = TestGames.Catalog.Get(c.View.Combat.State.Monsters[0].CardId);

        var cut = RenderTable(c);

        Assert.Contains($"Du {summary.PlayerStrength} : {summary.MonsterStrength} Monster", cut.Find(".strength").TextContent);
        Assert.Equal(summary.PlayerWinsTies, cut.Find(".strength").TextContent.Contains("Gleichstand gewinnt (QA)"));
        Assert.Contains($"{summary.TotalRewardLevels} Stufe(n), {summary.TotalRewardLoot} Loot", cut.Find(".combat-area").TextContent);
        Assert.Contains(monster.Name, cut.Find(".monsters").TextContent);
    }

    [Fact]
    public async Task HelpOfferAndAccept_ShowsHelper()
    {
        var (c, _) = await TestGames.InCombatAsync();
        var active = c.View.ActivePlayerId;
        var helper = OtherPlayer(c);
        c.BeginInterjection(helper);
        c.ConfirmHandoff();
        var cut = RenderTable(c);

        cut.Find(".help-panel input").Change("1");
        Button(cut, ".help-panel", "Hilfe anbieten").Click();

        Assert.Contains(c.View.Combat!.State.Offers, o => o.PlayerId == helper && o.LootShare == 1);

        Button(cut, ".table-band", "Fertig").Click();
        cut.FindAll("button").First(b => b.TextContent.Contains("aufdecken")).Click();
        Button(cut, ".help-panel", "Annehmen").Click();

        Assert.Equal(helper, c.View.Combat!.State.HelperId);
        Assert.Contains(NameOf(c, helper), cut.Find(".helper").TextContent);
        Assert.Equal(active, c.View.ActivePlayerId);
    }

    [Fact]
    public async Task Fleeing_ShowsRollButtonPerFleeingPlayer()
    {
        var time = new FakeTimeProvider();
        var (c, _) = await TestGames.InCombatAsync(time);
        var active = c.View.ActivePlayerId;
        var helper = OtherPlayer(c);
        await c.SendAsync(new OfferHelp(helper, 0));
        await c.SendAsync(new AcceptHelp(active, helper));
        await c.SendAsync(new ManualCombatAdjust(active, Side.Monster, 100));
        var cut = RenderTable(c);

        Button(cut, ".combat-area", "Kampf auswerten").Click();
        Assert.Contains("Letzte Chance! 5", cut.Find(".countdown").TextContent);
        time.Advance(Second);
        Assert.Contains("Letzte Chance! 4", cut.Find(".countdown").TextContent);
        for (var i = 0; i < 4; i++)
        {
            time.Advance(Second);
        }

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(CombatStage.Fleeing, c.View.Combat!.State.Stage);
            Assert.NotNull(Button(cut, ".fleeing", $"{NameOf(c, active)} würfelt"));
            Assert.NotNull(Button(cut, ".fleeing", $"{NameOf(c, helper)} würfelt"));
        });

        Button(cut, ".fleeing", $"{NameOf(c, helper)} würfelt").Click();

        var roll = c.LastEvents.OfType<EscapeRolled>().First();
        var text = cut.Find(".escape-results").TextContent;
        Assert.Contains(NameOf(c, helper), text);
        Assert.Contains(roll.Roll == 0 ? "ohne Wurf" : $"Wurf {roll.Roll} + {roll.Bonus}", text);
        Assert.DoesNotContain($"{NameOf(c, helper)} würfelt", string.Join("|", cut.FindAll(".fleeing button").Select(b => b.TextContent.Trim())));
    }

    [Fact]
    public async Task Choice_ShowsDialogForAffectedPlayer_AndResolves()
    {
        var c = await AtChoiceAsync();
        var choice = c.View.CurrentChoice!;
        var cut = RenderTable(c);

        var dialog = cut.Find(".choice-dialog");
        var name = NameOf(c, choice.PlayerId);
        Assert.Contains(choice.Kind == ChoiceKind.DiscardSmallItem
            ? $"{name}: Wirf ein kleines Item ab"
            : $"{name}: Gib ein angelegtes Item an {NameOf(c, choice.RecipientPlayerId!)}", dialog.TextContent);

        var owner = c.View.Players.First(p => p.Id == choice.PlayerId);
        var before = owner.Equipped.Count;
        var valid = owner.Equipped.First(x => choice.Kind == ChoiceKind.GiveEquippedItem
            || TestGames.Catalog.Get(x.CardId) is { Kind: CardKind.Equipment, Slot: Slot.None });
        cut.Find($".choice-dialog [data-instance='{valid.InstanceId}'] .card").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".choice-dialog")));
        Assert.Null(c.View.CurrentChoice);
        Assert.Equal(before - 1, c.View.Players.First(p => p.Id == choice.PlayerId).Equipped.Count);
    }

    [Fact]
    public void ManualEffectDialog_ShowsCardWithToolboxHint_AndCloses()
    {
        var card = TestGames.Catalog.Get("curse-unlocked-pc");
        var closed = false;
        Services.AddSingleton(CardArtCatalog.Parse("{}"));

        var cut = Render<ManualEffectDialog>(p => p
            .Add(x => x.Card, card)
            .Add(x => x.OnClose, () => closed = true));

        Assert.Contains("Setzt diese Karte mit dem Werkzeugkasten um.", cut.Find(".manual-dialog").TextContent);
        Assert.Contains(card.Name, cut.Find(".manual-dialog").TextContent);
        Assert.Contains(card.RulesText, cut.Find(".manual-dialog").TextContent);
        cut.Find(".manual-dialog button").Click();
        Assert.True(closed);
    }

    /// <summary>Der Fluch "Rechner nicht gesperrt" auf einen Spieler mit angelegter Karte erzeugt die Auswahl "Gib ein angelegtes Item".</summary>
    private static async Task<TableController> AtChoiceAsync()
    {
        for (ulong seed = 1; seed < 800; seed++)
        {
            var c = await TestGames.StartedAsync(3, seed);
            c.ConfirmHandoff();
            var active = c.View.ActivePlayerId;
            var curse = c.View.MyHand!.FirstOrDefault(x => x.CardId == "curse-unlocked-pc");
            if (curse is null)
            {
                continue;
            }

            var victim = c.View.Players.First(p => p.Id != active).Id;
            for (var id = 1; id < 300 && c.View.Players.First(p => p.Id == victim).Equipped.Count == 0; id++)
            {
                await c.SendAsync(new ManualMoveCard(active, id, victim, Zone.Equipped));
            }
            await c.SendAsync(new PlayCard(active, curse.InstanceId, victim));
            if (c.View.CurrentChoice is not null)
            {
                return c;
            }
        }
        throw new InvalidOperationException("Kein Seed mit offener Auswahl gefunden.");
    }
}
