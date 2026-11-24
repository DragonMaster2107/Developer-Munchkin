using Bunit;
using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Web.Components;
using DeveloperDungeon.Web.Logic;

namespace DeveloperDungeon.Web.Tests.Components;

public class TargetPickerTests : BunitContext
{
    [Fact]
    public void Side_OffersPlayersAndMonster()
    {
        ActionTarget? picked = null;
        var cut = Render<TargetPicker>(p => p
            .Add(x => x.Kind, TargetKind.Side)
            .Add(x => x.OnPick, t => picked = t));

        Assert.Equal(["Für die Spieler", "Für das Monster", "Abbrechen"], cut.FindAll("button").Select(b => b.TextContent.Trim()));

        cut.FindAll("button")[0].Click();

        Assert.Equal(new ActionTarget(Side: Side.Player), picked);
    }
}
