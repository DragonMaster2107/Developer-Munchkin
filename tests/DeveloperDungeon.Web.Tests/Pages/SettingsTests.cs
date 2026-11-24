using Bunit;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Pages;
using DeveloperDungeon.Web.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace DeveloperDungeon.Web.Tests.Pages;

public class SettingsTests : BunitContext
{
    [Fact]
    public async Task ChangingCountdown_IsSaved()
    {
        var storage = new InMemoryGameStorage();
        Services.AddSingleton<IGameStorage>(storage);
        Services.AddScoped<SettingsStore>();

        var cut = Render<Settings>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input[type=number]")));
        cut.Find("input[type=number]").Change("8");

        var saved = await new SettingsStore(storage).LoadAsync();
        Assert.Equal(8, saved.CountdownSeconds);
        Assert.True(saved.AnimationsEnabled);
    }
}
