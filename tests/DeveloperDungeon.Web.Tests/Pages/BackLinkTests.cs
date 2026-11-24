using Bunit;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Pages;
using DeveloperDungeon.Web.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DeveloperDungeon.Web.Tests.Pages;

public class BackLinkTests : BunitContext
{
    [Fact]
    public void Gallery_HasBackLinkToStartPage()
    {
        Services.AddSingleton(TestGames.Catalog);
        Services.AddSingleton(CardArtCatalog.Parse("{}"));

        var cut = Render<Gallery>();

        Assert.Equal("", cut.FindAll("a").Single(a => a.TextContent.Trim() == "Zurück").GetAttribute("href"));
    }

    [Fact]
    public void NotFound_HasBackLinkToStartPage()
    {
        var cut = Render<NotFound>();

        Assert.Equal("", cut.FindAll("a").Single(a => a.TextContent.Trim() == "Zurück").GetAttribute("href"));
    }
}
