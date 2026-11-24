using Bunit;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Pages;
using DeveloperDungeon.Web.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DeveloperDungeon.Web.Tests;

public class StartupTests : BunitContext
{
    [Fact]
    public void Home_ShowsGameTitle()
    {
        Services.AddSingleton<IGameStorage>(new InMemoryGameStorage());
        Services.AddSingleton(TestGames.Catalog);
        Services.AddSingleton<TimeProvider>(TimeProvider.System);
        Services.AddScoped<TableController>();

        var cut = Render<Home>();

        Assert.Equal("Developer Dungeon", cut.Find("h1").TextContent.Trim());
    }
}
