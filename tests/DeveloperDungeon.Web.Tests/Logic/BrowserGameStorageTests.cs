using Bunit;
using DeveloperDungeon.Web.Logic;
using Microsoft.JSInterop;

namespace DeveloperDungeon.Web.Tests.Logic;

public class BrowserGameStorageTests : BunitContext
{
    private BrowserGameStorage CreateSut() => new(JSInterop.JSRuntime);

    [Fact]
    public async Task Load_ReturnsStoredValue()
    {
        JSInterop.Setup<string?>("localStorage.getItem", StorageKeys.Save).SetResult("x");

        Assert.Equal("x", await CreateSut().LoadAsync(StorageKeys.Save));
    }

    [Fact]
    public async Task Save_CallsSetItem()
    {
        JSInterop.SetupVoid("localStorage.setItem", StorageKeys.Save, "abc").SetVoidResult();

        await CreateSut().SaveAsync(StorageKeys.Save, "abc");

        var call = Assert.Single(JSInterop.Invocations, i => i.Identifier == "localStorage.setItem");
        Assert.Equal(new object?[] { StorageKeys.Save, "abc" }, call.Arguments);
    }

    [Fact]
    public async Task Load_WhenJsThrows_ReturnsNull()
    {
        JSInterop.Setup<string?>("localStorage.getItem", StorageKeys.Save).SetException(new JSException("blocked"));

        Assert.Null(await CreateSut().LoadAsync(StorageKeys.Save));
    }
}
