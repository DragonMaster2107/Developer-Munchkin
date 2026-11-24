using Bunit;
using DeveloperDungeon.Web.Pages;

namespace DeveloperDungeon.Web.Tests;

public class StartupTests : BunitContext
{
    [Fact]
    public void Home_ShowsGameTitle()
    {
        var cut = Render<Home>();

        Assert.Equal("Developer Dungeon", cut.Find("h1").TextContent.Trim());
    }
}
