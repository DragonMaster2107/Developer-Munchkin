using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Tests.Support;

namespace DeveloperDungeon.Web.Tests.Logic;

public class SettingsStoreTests
{
    private readonly InMemoryGameStorage _storage = new();

    private SettingsStore CreateSut() => new(_storage);

    [Fact]
    public async Task Load_Empty_ReturnsDefaults()
    {
        var s = await CreateSut().LoadAsync();

        Assert.True(s.AnimationsEnabled);
        Assert.Equal(5, s.CountdownSeconds);
    }

    [Fact]
    public async Task Save_ThenLoad_RoundTrips()
    {
        var sut = CreateSut();
        await sut.SaveAsync(new GameSettings(false, 3));

        Assert.Equal(new GameSettings(false, 3), await sut.LoadAsync());
    }

    [Fact]
    public async Task Load_InvalidJson_ReturnsDefaults()
    {
        _storage.Items[StorageKeys.Settings] = "{nicht json";

        Assert.Equal(new GameSettings(), await CreateSut().LoadAsync());
    }

    [Fact]
    public async Task Load_ClampsCountdownToTen()
    {
        _storage.Items[StorageKeys.Settings] = "{\"AnimationsEnabled\":true,\"CountdownSeconds\":99}";

        Assert.Equal(10, (await CreateSut().LoadAsync()).CountdownSeconds);
    }
}
