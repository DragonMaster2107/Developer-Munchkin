using Microsoft.JSInterop;

namespace DeveloperDungeon.Web.Logic;

public sealed class BrowserGameStorage(IJSRuntime js) : IGameStorage
{
    public async Task<string?> LoadAsync(string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task SaveAsync(string key, string value) =>
        await js.InvokeVoidAsync("localStorage.setItem", key, value);

    public async Task RemoveAsync(string key) =>
        await js.InvokeVoidAsync("localStorage.removeItem", key);
}
