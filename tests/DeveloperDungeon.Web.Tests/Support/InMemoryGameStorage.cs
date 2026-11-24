using DeveloperDungeon.Web.Logic;

namespace DeveloperDungeon.Web.Tests.Support;

public sealed class InMemoryGameStorage : IGameStorage
{
    public Dictionary<string, string> Items { get; } = new();

    public Task<string?> LoadAsync(string key) =>
        Task.FromResult(Items.TryGetValue(key, out var v) ? v : null);

    public Task SaveAsync(string key, string value)
    {
        Items[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        Items.Remove(key);
        return Task.CompletedTask;
    }
}
