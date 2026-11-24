using Bunit;
using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Views;
using DeveloperDungeon.Web.Components;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DeveloperDungeon.Web.Tests.Components;

public class ChoiceDialogTests : BunitContext
{
    private static readonly CardCatalog Catalog = CardCatalog.FromJson("""
        [
          { "id": "equip-duck", "name": "Gummiente", "deck": "loot", "kind": "equipment", "count": 1, "slot": "none", "hands": 0, "bonus": 1, "value": 100 },
          { "id": "equip-hat", "name": "Hut", "deck": "loot", "kind": "equipment", "count": 1, "slot": "head", "hands": 0, "bonus": 1, "value": 100 }
        ]
        """);

    private IRenderedComponent<ChoiceDialog> RenderChoice(ChoiceKind kind)
    {
        var controller = TestGames.NewController();
        Services.AddSingleton(controller);
        Services.AddSingleton(CardArtCatalog.Parse("{}"));

        var baseView = controller.View;
        var owner = baseView.Players[0] with
        {
            Equipped = [new CardInstance { InstanceId = 101, CardId = "equip-duck" }, new CardInstance { InstanceId = 102, CardId = "equip-hat" }],
        };
        var choice = new PendingChoice { PlayerId = owner.Id, Kind = kind, RecipientPlayerId = baseView.Players[1].Id, SourceCardId = "x" };
        GameView view = baseView with { Players = [owner, .. baseView.Players.Skip(1)], CurrentChoice = choice };

        return Render<ChoiceDialog>(p => p.Add(x => x.View, view).Add(x => x.Catalog, Catalog));
    }

    private static int[] Offered(IRenderedComponent<ChoiceDialog> cut) =>
        [.. cut.FindAll(".choice-card").Select(e => int.Parse(e.GetAttribute("data-instance")!))];

    [Fact]
    public void DiscardSmallItem_OffersOnlySmallItems()
    {
        Assert.Equal([101], Offered(RenderChoice(ChoiceKind.DiscardSmallItem)));
    }

    [Fact]
    public void GiveEquippedItem_OffersAllEquipped()
    {
        Assert.Equal([101, 102], Offered(RenderChoice(ChoiceKind.GiveEquippedItem)));
    }
}
