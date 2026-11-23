using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Views;

namespace DeveloperDungeon.Engine.Sessions;

public interface IGameSession
{
    GameView GetView(string? viewerId);
    CardCatalog Catalog { get; }
    Task<CommandResult> SendAsync(GameCommand command);

    /// <summary>Prüft, ob der Befehl angenommen würde, ohne das Spiel zu verändern (z. B. zum Ausgrauen mit Begründung).</summary>
    Task<CommandResult> CheckAsync(GameCommand command);

    /// <summary>Nach jedem angenommenen Befehl; trägt dessen Ereignisse.</summary>
    event Action<IReadOnlyList<GameEvent>>? StateChanged;
}
