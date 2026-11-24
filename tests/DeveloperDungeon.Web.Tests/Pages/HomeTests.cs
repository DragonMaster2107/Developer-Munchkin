using Bunit;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Pages;
using DeveloperDungeon.Web.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DeveloperDungeon.Web.Tests.Pages;

public class HomeTests : BunitContext
{
    private IRenderedComponent<Home> RenderHome(InMemoryGameStorage storage)
    {
        Services.AddSingleton<IGameStorage>(storage);
        Services.AddSingleton(TestGames.Catalog);
        Services.AddScoped<TableController>();
        return Render<Home>();
    }

    [Fact]
    public void NoSave_HidesResume()
    {
        var cut = RenderHome(new InMemoryGameStorage());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("a[href='neu']")));
        Assert.DoesNotContain("Fortsetzen", cut.Markup);
        Assert.DoesNotContain("passt nicht mehr", cut.Markup);
    }

    [Fact]
    public void ValidSave_ShowsResume()
    {
        var storage = new InMemoryGameStorage();
        TestGames.NewController(3, 7, storage);

        var cut = RenderHome(storage);

        cut.WaitForAssertion(() => Assert.Contains("Fortsetzen", cut.Markup));
        cut.FindAll("button").Single(b => b.TextContent.Contains("Fortsetzen")).Click();
        Assert.EndsWith("/spiel", Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri);
    }

    [Fact]
    public void StartPage_CorruptSave_ShowsHintAndOffersNewGameOnly()
    {
        var storage = new InMemoryGameStorage();
        storage.Items[StorageKeys.Save] = "kein json {";

        var cut = RenderHome(storage);

        cut.WaitForAssertion(() => Assert.Contains("Der gespeicherte Spielstand passt nicht mehr zu dieser Version.", cut.Markup));
        Assert.DoesNotContain("Fortsetzen", cut.Markup);
        Assert.NotEmpty(cut.FindAll("a[href='neu']"));
    }
}
