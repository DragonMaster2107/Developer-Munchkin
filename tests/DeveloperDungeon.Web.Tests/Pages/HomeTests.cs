using Bunit;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.State;
using Microsoft.Extensions.Time.Testing;
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
        Services.AddSingleton<TimeProvider>(TimeProvider.System);
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

    [Fact]
    public async Task InMemoryGameWithSaveWarning_IsNotReloaded_ShowsWarningAndResume()
    {
        var storage = new FailingSaveStorage();
        var controller = new TableController(TestGames.Catalog, storage, new FakeTimeProvider());
        await controller.StartNewGameAsync(TestGames.Setups(3), 7);
        Assert.NotNull(controller.LastWarning);
        Services.AddSingleton<IGameStorage>(storage);
        Services.AddSingleton(TestGames.Catalog);
        Services.AddSingleton<TimeProvider>(TimeProvider.System);
        Services.AddSingleton(controller);

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains(TableController.SaveFailedWarning, cut.Markup));
        Assert.Equal(0, storage.Loads);
        Assert.Contains("Fortsetzen", cut.Markup);
        Assert.True(controller.HasGame);
    }

    [Fact]
    public async Task FinishedGameSave_OffersLastGameInsteadOfResume()
    {
        var storage = new InMemoryGameStorage();
        var c = await TestGames.StartedAsync(3, 42, storage);
        c.ConfirmHandoff();
        var active = c.View.ActivePlayerId;
        await c.SendAsync(new ManualAdjustLevel(active, active, 9));
        Assert.Equal(GamePhase.GameOver, c.View.Phase);

        var cut = RenderHome(storage);

        cut.WaitForAssertion(() => Assert.Contains("Letzte Partie ansehen", cut.Markup));
        Assert.DoesNotContain("Fortsetzen", cut.Markup);
    }

    private sealed class FailingSaveStorage : IGameStorage
    {
        public int Loads { get; private set; }

        public Task<string?> LoadAsync(string key)
        {
            Loads++;
            return Task.FromResult<string?>(null);
        }

        public Task SaveAsync(string key, string value) => throw new InvalidOperationException("Quota");
        public Task RemoveAsync(string key) => Task.CompletedTask;
    }
}
