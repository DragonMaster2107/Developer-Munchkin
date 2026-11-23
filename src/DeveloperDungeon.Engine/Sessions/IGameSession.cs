using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Views;

namespace DeveloperDungeon.Engine.Sessions;

public interface IGameSession
{
    GameView GetView(string? viewerId);
    CardCatalog Catalog { get; }
    Task<CommandResult> SendAsync(GameCommand command);
    event Action? StateChanged;
}
