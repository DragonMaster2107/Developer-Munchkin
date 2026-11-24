namespace DeveloperDungeon.Web.Logic;

public interface IGameStorage
{
    Task<string?> LoadAsync(string key);
    Task SaveAsync(string key, string value);
    Task RemoveAsync(string key);
}

public static class StorageKeys
{
    public const string Save = "dd.save.v1";
    public const string Settings = "dd.settings.v1";
}
