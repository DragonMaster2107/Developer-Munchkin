using System.Text.Json;

namespace DeveloperDungeon.Web.Logic;

public sealed class SettingsStore(IGameStorage storage)
{
    public async Task<GameSettings> LoadAsync()
    {
        var json = await storage.LoadAsync(StorageKeys.Settings);
        if (string.IsNullOrWhiteSpace(json))
            return new GameSettings();
        try
        {
            var s = JsonSerializer.Deserialize(json, WebJsonContext.Default.GameSettings);
            return s is null ? new GameSettings() : s with { CountdownSeconds = Math.Clamp(s.CountdownSeconds, 0, 10) };
        }
        catch (JsonException)
        {
            return new GameSettings();
        }
    }

    public Task SaveAsync(GameSettings settings) =>
        storage.SaveAsync(StorageKeys.Settings, JsonSerializer.Serialize(settings, WebJsonContext.Default.GameSettings));
}
